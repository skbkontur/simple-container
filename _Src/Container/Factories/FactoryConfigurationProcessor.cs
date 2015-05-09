using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using SimpleContainer.Configuration;
using SimpleContainer.Generics;
using SimpleContainer.Helpers;

namespace SimpleContainer.Factories
{
	internal class FactoryConfigurationProcessor
	{
		private readonly ConcurrentDictionary<Type, Func<object, object>> genericFactories =
			new ConcurrentDictionary<Type, Func<object, object>>();

		public void FirstRun(ContainerConfigurationBuilder builder, Type type)
		{
			var targetTypes = type.GetConstructors().SelectMany(x => x.GetParameters())
				.Select(x => x.ParameterType)
				.Where(targetType => targetType.IsGenericType);
			foreach (var targetType in targetTypes)
				if (targetType.GetGenericTypeDefinition() == typeof (Func<,>))
					ConfigureAsSimpleFactory(builder, targetType, type);
				else if (targetType.GetGenericTypeDefinition() == typeof (Func<,,>))
					ConfigureAsGenericFactory(builder, targetType.GetGenericArguments(), type);
		}

		private void ConfigureAsGenericFactory(ContainerConfigurationBuilder containerConfigurator,
			Type[] factoryArgumentTypes, Type pluggableType)
		{
			if (factoryArgumentTypes[0] != typeof (Type))
				return;
			if (factoryArgumentTypes[1] != typeof (object))
				return;
			var serviceType = factoryArgumentTypes[2];

			var implementationType = GetImplementationDefinitionOrNull(serviceType, pluggableType);
			if (implementationType == null)
				return;
			containerConfigurator.BindDependency(pluggableType,
				typeof (Func<,,>).MakeGenericType(typeof (Type), typeof (object), serviceType),
				c => CreateGenericFactory(serviceType, implementationType, c));
		}

		private static Type GetImplementationDefinitionOrNull(Type serviceType, Type pluggableType)
		{
			var implementationTypes = pluggableType.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)
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

		private Delegate CreateGenericFactory(Type serviceType, Type implementationDefinition, IContainer container)
		{
			return DelegateCaster.Create(serviceType)
				.Cast((type, o) => GetGenericFactoryInternal(implementationDefinition.MakeGenericType(type), container)(o));
		}

		private Func<object, object> GetGenericFactoryInternal(Type type, IContainer mtContainer)
		{
			return genericFactories.GetOrAdd(type, it => CreateFactory(it, mtContainer));
		}

		private void ConfigureAsSimpleFactory(ContainerConfigurationBuilder builder, Type targetType, Type pluggableType)
		{
			var arguments = targetType.GetGenericArguments();
			if (arguments[0] != typeof (object))
				return;
			var serviceType = arguments[1];
			if (!serviceType.IsAbstract)
				return;
			var implementationDefinition = GetImplementationDefinitionOrNull(serviceType, pluggableType);
			if (implementationDefinition == null)
				return;
			var closingParameters = implementationDefinition.GetConstructors().Single()
				.GetParameters()
				.Where(p => p.ParameterType.ContainsGenericParameters)
				.ToArray();

			if (closingParameters.Length != 1)
				return;

			builder.BindDependency(pluggableType,
				typeof (Func<,>).MakeGenericType(typeof (object), serviceType),
				c => CreateAutoClosingFactory(serviceType, implementationDefinition, closingParameters[0], c));
		}

		private Delegate CreateAutoClosingFactory(Type serviceType, Type implementationDefinition,
			ParameterInfo parameter, IContainer container)
		{
			Func<object, object> f = delegate(object o)
			{
				var accessor = ObjectAccessor.Get(o);
				object parameterValue;
				if (!accessor.TryGet(parameter.Name, out parameterValue))
					throw new InvalidOperationException("can't detect type of " + implementationDefinition.FormatName());
				var makeGenericTypes = implementationDefinition.CloseBy(parameter.ParameterType, parameterValue.GetType())
					.ToArray();
				if (makeGenericTypes.Length > 1)
					throw new NotSupportedException(
						string.Format("cannot auto close type {0} with multiple interfaces on parameter {1} for serviceType {2}",
							implementationDefinition, parameter.ParameterType, serviceType));
				return GetGenericFactoryInternal(makeGenericTypes[0], container)(o);
			};
			return DelegateCaster.Create(serviceType).Cast(f);
		}

		private static Func<object, object> CreateFactory(Type type, IContainer container)
		{
			var constructor = type.GetConstructors().Single();
			var parameters = constructor.GetParameters().ToArray();
			var constructorInvoker = ReflectionHelpers.EmitCallOf(constructor);

			return delegate(object o)
			{
				var accessor = ObjectAccessor.Get(o);
				var parameterValues = new object[parameters.Length];
				for (var i = 0; i < parameterValues.Length; i++)
				{
					object parameterValue;
					var parameter = parameters[i];
					if (!accessor.TryGet(parameter.Name, out parameterValue))
					{
						var resolved = container.GetAll(parameter.ParameterType).ToArray();
						if (resolved.Length == 1)
							parameterValue = resolved[0];
						else if (parameter.HasDefaultValue)
							parameterValue = parameter.DefaultValue;
						else
							container.Get(parameter.ParameterType, null);
					}
					parameterValues[i] = parameterValue;
				}

				return constructorInvoker(null, parameterValues);
			};
		}
	}
}