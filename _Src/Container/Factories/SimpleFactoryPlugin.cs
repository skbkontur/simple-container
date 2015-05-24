using System;
using System.Linq;
using System.Reflection;
using SimpleContainer.Generics;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation;

namespace SimpleContainer.Factories
{
	internal class SimpleFactoryPlugin : IFactoryPlugin
	{
		public bool TryInstantiate(ContainerService.Builder builder)
		{
			if (!builder.Type.IsGenericType)
				return false;
			if (builder.Type.GetGenericTypeDefinition() != typeof (Func<>))
				return false;
			var type = builder.Type.GetGenericArguments()[0];
			var baseFactory = FactoryCreator.CreateFactory(builder);
			Func<object> factory = () => baseFactory(type, null);
			builder.AddInstance(DelegateCaster.Create(type).Cast(factory), true);
			return true;
		}
	}

	internal abstract class HujBase
	{
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
	}

	internal class GenericFactoryPlugin : HujBase, IFactoryPlugin
	{
		public bool TryInstantiate(ContainerService.Builder builder)
		{
			if (!builder.Type.IsGenericType)
				return false;
			if (builder.Type.GetGenericTypeDefinition() != typeof (Func<,,>))
				return false;
			var factoryArgumentTypes = builder.Type.GetGenericArguments();
			if (factoryArgumentTypes[0] != typeof (Type))
				return false;
			if (factoryArgumentTypes[1] != typeof (object))
				return false;
			var type = factoryArgumentTypes[2];
			var hostService = builder.Context.GetPreviousService();
			var implementationType = GetImplementationDefinitionOrNull(type, hostService.Type);
			if (implementationType == null)
				return false;
			var baseFactory = FactoryCreator.CreateFactory(builder);
			Func<Type, object, object> factory = (t, o) => baseFactory(implementationType.MakeGenericType(t), o);
			builder.AddInstance(DelegateCaster.Create(type).Cast(factory), true);
			return true;
		}
	}
	
	internal class GenericMegaFactoryPlugin : HujBase, IFactoryPlugin
	{
		public bool TryInstantiate(ContainerService.Builder builder)
		{
			if (!builder.Type.IsGenericType)
				return false;
			if (builder.Type.GetGenericTypeDefinition() != typeof (Func<,>))
				return false;
			var factoryArgumentTypes = builder.Type.GetGenericArguments();
			if (factoryArgumentTypes[0] != typeof (object))
				return false;
			var type = factoryArgumentTypes[1];
			var hostService = builder.Context.GetPreviousService();
			var implementationType = GetImplementationDefinitionOrNull(type, hostService.Type);
			if (implementationType == null)
				return false;
			var closingParameters = implementationType.GetConstructors().Single()
				.GetParameters()
				.Where(p => p.ParameterType.ContainsGenericParameters)
				.ToArray();
			if (closingParameters.Length != 1)
				return false;
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
			builder.AddInstance(DelegateCaster.Create(type).Cast(factory), true);
			return true;
		}
	}
}