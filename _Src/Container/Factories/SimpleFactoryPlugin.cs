using System;
using System.Reflection;
using SimpleContainer.Implementation;
using SimpleContainer.Implementation.Hacks;

namespace SimpleContainer.Factories
{
	internal class SimpleFactoryPlugin : IFactoryPlugin
	{
		public bool TryInstantiate(Implementation.SimpleContainer container, ContainerService containerService)
		{
			if (!containerService.Type.GetTypeInfo().IsGenericType)
				return false;
			if (containerService.Type.GetGenericTypeDefinition() != typeof (Func<>))
				return false;
			var type = containerService.Type.GetGenericArguments()[0];
			var factoryWithArguments = FactoryCreator.CreateFactory(type, container, containerService);
			Func<object> factory = () => factoryWithArguments(null);
			containerService.AddInstance(DelegateCaster.Create(type).Cast(factory), true);
			return true;
		}
	}
}