using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Generics;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation;

namespace SimpleContainer.Factories
{
	internal class FactoryPlugin : IFactoryPlugin
	{
		public bool TryInstantiate(ContainerService.Builder builder)
		{
			if (!builder.Type.IsGenericType || !typeof (Delegate).IsAssignableFrom(builder.Type))
				return false;
			foreach (var factoryType in factoryTypes)
			{
				if (builder.Type.GetGenericTypeDefinition() != factoryType.FuncType)
					continue;
				var arguments = builder.Type.GetGenericArguments();
				if (arguments.Length != factoryType.FormalParameterTypes.Length + 1)
					continue;
				if (!arguments.StartsWith(factoryType.FormalParameterTypes, EqualityComparer<Type>.Default))
					continue;
				var resultType = arguments[arguments.Length - 1];
				var factory = factoryType.TryCreate(resultType, builder);
				if (factory == null)
					continue;
				builder.AddInstance(factory, true);
				return true;
			}
			return false;
		}

		private static FactoryType F(params Type[] types)
		{
			return new FactoryType(types);
		}

		private readonly FactoryType[] factoryTypes =
		{
			F().CreateBy(delegate(Type type, ContainerService.Builder builder)
			{
				var baseFactory = FactoryCreator.CreateFactory(builder);
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
				var baseFactory = FactoryCreator.CreateFactory(builder);
				Func<object, object> factory = delegate(object o)
				{
					var accessor = ObjectAccessor.Get(o);
					object parameterValue;
					if (!accessor.TryGet(parameter.Name, out parameterValue))
						throw new InvalidOperationException("can't detect type of " + implementationType.FormatName());
					var makeGenericTypes = implementationType.CloseBy(parameter.ParameterType, parameterValue.GetType())
						.ToArray();
					if (makeGenericTypes.Length > 1)
						throw new NotSupportedException(
							string.Format("cannot auto close type {0} with multiple interfaces on parameter {1} for serviceType {2}",
								implementationType, parameter.ParameterType, hostService.Type));
					return baseFactory(makeGenericTypes[0], o);
				};
				return DelegateCaster.Create(type).Cast(factory);
			}),
			F(typeof (object)).CreateBy(delegate(Type type, ContainerService.Builder builder)
			{
				var baseFactory = FactoryCreator.CreateFactory(builder);
				Func<object, object> factory = o => baseFactory(type, o);
				return DelegateCaster.Create(type).Cast(factory);
			}),
			F(typeof (Type), typeof (object)).CreateBy(delegate(Type type, ContainerService.Builder builder)
			{
				var hostService = builder.Context.GetPreviousService();
				var implementationType = GetImplementationDefinitionOrNull(type, hostService.Type);
				if (implementationType == null)
					return null;
				var baseFactory = FactoryCreator.CreateFactory(builder);
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

		private class FactoryType
		{
			public FactoryType(params Type[] types)
			{
				FormalParameterTypes = types;
				FuncType = GetFuncType(types.Length);
			}

			public FactoryType CreateBy(Func<Type, ContainerService.Builder, Delegate> creator)
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