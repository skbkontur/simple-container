using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Generics;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation;
using SimpleContainer.Interface;

namespace SimpleContainer.Factories
{
	internal class FactoryPlugin : IFactoryPlugin
	{
		public bool TryInstantiate(ContainerService.Builder builder)
		{
			if (!builder.Type.IsGenericType || !typeof (Delegate).IsAssignableFrom(builder.Type))
				return false;
			foreach (var creator in factoryCreators)
			{
				if (builder.Type.GetGenericTypeDefinition() != creator.FuncType)
					continue;
				var arguments = builder.Type.GetGenericArguments();
				if (arguments.Length != creator.FormalParameterTypes.Length + 1)
					continue;
				if (!arguments.StartsWith(creator.FormalParameterTypes, EqualityComparer<Type>.Default))
					continue;
				var resultType = arguments[arguments.Length - 1];
				var factory = creator.TryCreate(resultType, builder);
				if (factory == null)
					continue;
				builder.AddInstance(factory, true);
				return true;
			}
			return false;
		}

		private static FactoryCreator F(params Type[] types)
		{
			return new FactoryCreator(types);
		}

		private readonly FactoryCreator[] factoryCreators =
		{
			F().CreateBy(delegate(Type type, ContainerService.Builder builder)
			{
				var baseFactory = CreateFactory(builder);
				Func<object> factory = () => baseFactory(type, null);
				return DelegateCaster.Create(type).Cast(factory);
			}),
			F(typeof (object)).CreateBy(delegate(Type type, ContainerService.Builder builder)
			{
				var hostService = builder.Context.GetPreviousService();
				var implementationType = GetImplementationDefinitionOrNull(type, hostService.Type);
				if (implementationType == null)
					return null;
				var closingParameters = implementationType.GetConstructors().Single()
					.GetParameters()
					.Where(p => p.ParameterType.ContainsGenericParameters)
					.ToArray();
				if (closingParameters.Length != 1)
					return null;
				var parameter = closingParameters[0];
				var baseFactory = CreateFactory(builder);
				Func<object, object> factory = delegate(object o)
				{
					var accessor = ObjectAccessor.Get(o);
					object parameterValue;
					if (!accessor.TryGet(parameter.Name, out parameterValue))
						throw new InvalidOperationException("can't detect type of " + implementationType.FormatName());
					var closedTypes = implementationType.CloseBy(parameter.ParameterType, parameterValue.GetType()).ToArray();
					if (closedTypes.Length > 1)
						throw new NotSupportedException(
							string.Format("cannot auto close type {0} with multiple interfaces on parameter {1} for serviceType {2}",
								implementationType, parameter.ParameterType, hostService.Type));
					return baseFactory(closedTypes[0], o);
				};
				return DelegateCaster.Create(type).Cast(factory);
			}),
			F(typeof (object)).CreateBy(delegate(Type type, ContainerService.Builder builder)
			{
				var baseFactory = CreateFactory(builder);
				Func<object, object> factory = o => baseFactory(type, o);
				return DelegateCaster.Create(type).Cast(factory);
			}),
			F(typeof (Type), typeof (object)).CreateBy(delegate(Type type, ContainerService.Builder builder)
			{
				var hostService = builder.Context.GetPreviousService();
				var implementationType = GetImplementationDefinitionOrNull(type, hostService.Type);
				if (implementationType == null)
					return null;
				var baseFactory = CreateFactory(builder);
				Func<Type, object, object> factory = (t, o) => baseFactory(implementationType.MakeGenericType(t), o);
				return DelegateCaster.Create(type).Cast(factory);
			})
		};

		protected static Type GetImplementationDefinitionOrNull(Type serviceType, Type hostType)
		{
			var implementationTypes = hostType.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)
				.Where(serviceType.IsAssignableFrom)
				.Where(x => !x.IsAbstract)
				.ToArray();
			if (implementationTypes.Length != 1)
				return null;
			var implementationType = implementationTypes[0];
			if (!implementationType.IsGenericTypeDefinition || implementationType.GetGenericArguments().Length != 1)
				return null;
			return implementationType;
		}

		private static Func<Type, object, object> CreateFactory(ContainerService.Builder builder)
		{
			var declaredContractNames = builder.DeclaredContracts;
			var hostService = builder.Context.GetPreviousService();
			builder.UseAllDeclaredContracts();
			return delegate(Type type, object arguments)
			{
				if (hostService == null || hostService != builder.Context.GetTopService())
				{
					var resolvedService = builder.Container.Create(type, declaredContractNames, arguments);
					resolvedService.Run();
					return resolvedService.Single();
				}
				var result = builder.Container.Create(type, declaredContractNames, arguments, builder.Context);
				var resultDependency = result.AsSingleInstanceDependency("() => " + result.Type.FormatName());
				hostService.AddDependency(resultDependency, false);
				if (resultDependency.Status != ServiceStatus.Ok)
					throw new ServiceCouldNotBeCreatedException();
				return resultDependency.Value;
			};
		}

		private class FactoryCreator
		{
			public FactoryCreator(params Type[] types)
			{
				FormalParameterTypes = types;
				FuncType = GetFuncType(types.Length);
			}

			public FactoryCreator CreateBy(Func<Type, ContainerService.Builder, Delegate> creator)
			{
				TryCreate = creator;
				return this;
			}

			private static Type GetFuncType(int parametersCount)
			{
				switch (parametersCount)
				{
					case 0:
						return typeof (Func<>);
					case 1:
						return typeof (Func<,>);
					case 2:
						return typeof (Func<,,>);
					default:
						throw new InvalidOperationException(string.Format("unexpected parametersCount [{0}]", parametersCount));
				}
			}

			public Type FuncType { get; private set; }
			public Type[] FormalParameterTypes { get; private set; }
			public Func<Type, ContainerService.Builder, Delegate> TryCreate { get; private set; }
		}
	}
}