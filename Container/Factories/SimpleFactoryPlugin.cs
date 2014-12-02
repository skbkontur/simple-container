using System;
using SimpleContainer.Implementation;

namespace SimpleContainer.Factories
{
	internal class SimpleFactoryPlugin : IFactoryPlugin
	{
		public bool TryInstantiate(IContainer container, ContainerService containerService)
		{
			if (!containerService.type.IsGenericType)
				return false;
			if (containerService.type.GetGenericTypeDefinition() != typeof (Func<>))
				return false;
			var type = containerService.type.GetGenericArguments()[0];
			var contract = containerService.context.ContractsKey;
			Func<object> factory = () => container.Create(type, contract, null);
			containerService.instances.Add(DelegateCaster.Create(type).Cast(factory));
			containerService.usedContractName = contract;
			return true;
		}
	}
}