using System;
using System.Linq;
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
			var requiredContractNames = containerService.context.requiredContracts.Select(x => x.name).ToArray();
			Func<object> factory = () => container.Create(type, requiredContractNames, null);
			containerService.AddInstance(DelegateCaster.Create(type).Cast(factory));
			containerService.UseAllContracts(requiredContractNames.Length);
			return true;
		}
	}
}