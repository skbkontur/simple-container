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
			var requiredContractNames = containerService.Context.RequiredContractNames();
			var hostService = containerService.Context.GetPreviousService();
			Func<object> factory = () =>
			{
				var topService = containerService.Context.GetTopService();
				return container.Create(type, requiredContractNames, null,
					topService == hostService ? containerService.Context : null);
			};
			containerService.AddInstance(DelegateCaster.Create(type).Cast(factory));
			containerService.UseAllContracts(requiredContractNames.Length);
			return true;
		}
	}
}