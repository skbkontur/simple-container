using System;
using SimpleContainer.Implementation;

namespace SimpleContainer.Factories
{
	internal class SimpleFactoryPlugin : IFactoryPlugin
	{
		public bool TryInstantiate(Implementation.SimpleContainer container, ContainerService containerService)
		{
			if (!containerService.Type.IsGenericType)
				return false;
			if (containerService.Type.GetGenericTypeDefinition() != typeof (Func<>))
				return false;
			var type = containerService.Type.GetGenericArguments()[0];
			var factoryWithArguments = FactoryCreator.CreateFactory(type, container, containerService);
			Func<object> factory = () => factoryWithArguments(null);
			containerService.AddInstance(DelegateCaster.Create(type).Cast(factory));
			return true;
		}
	}
}