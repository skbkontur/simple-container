using System;
using SimpleContainer.Implementation;

namespace SimpleContainer.Factories
{
	public class SimpleFactoryPlugin : IFactoryPlugin
	{
		public bool TryInstantiate(IContainer container, ContainerService containerService)
		{
			if (!containerService.type.IsGenericType)
				return false;
			if (containerService.type.GetGenericTypeDefinition() != typeof (Func<>))
				return false;
			var type = containerService.type.GetGenericArguments()[0];
			var contract = containerService.context.Contract;
			Func<object> factory = () => container.Create(type, contract, null);
			containerService.instances.Add(DelegateCaster.Create(type).Cast(factory));
			containerService.contractUsed = true;
			return true;
		}
	}
}