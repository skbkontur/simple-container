using System;
using SimpleContainer.Implementation;

namespace SimpleContainer.Factories
{
	internal class SimpleFactoryPlugin : IFactoryPlugin
	{
		public bool TryInstantiate(IContainer container, ContainerService containerService)
		{
			if (!containerService.Type.IsGenericType)
				return false;
			if (containerService.Type.GetGenericTypeDefinition() != typeof(Func<>))
				return false;
			var type = containerService.Type.GetGenericArguments()[0];
			var requiredContractNames = containerService.Context.RequiredContractNames();
			Func<object> factory = () => container.Create(type, requiredContractNames, null);
			containerService.AddInstance(DelegateCaster.Create(type).Cast(factory));
			containerService.UseAllContracts(requiredContractNames.Length);
			return true;
		}
	}
}