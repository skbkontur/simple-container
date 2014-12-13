using System;
using SimpleContainer.Implementation;

namespace SimpleContainer.Factories
{
	//todo remove duplicate with FactoryWithArgumentsPlugin
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
				if (topService != hostService)
					return container.Create(type, requiredContractNames, null);
				var dependency = container.Create(type, requiredContractNames, null, containerService.Context);
				containerService.Dependencies.Add(dependency);
				return dependency.SingleInstance(true);
			};
			containerService.AddInstance(DelegateCaster.Create(type).Cast(factory));
			containerService.UseAllRequiredContracts();
			return true;
		}
	}
}