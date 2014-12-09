using System;
using SimpleContainer.Implementation;

namespace SimpleContainer.Factories
{
	internal class FactoryWithArgumentsPlugin : IFactoryPlugin
	{
		public bool TryInstantiate(Implementation.SimpleContainer container, ContainerService containerService)
		{
			var funcType = containerService.Type;
			if (!funcType.IsGenericType)
				return false;
			if (funcType.GetGenericTypeDefinition() != typeof (Func<,>))
				return false;
			var typeArguments = funcType.GetGenericArguments();
			if (typeArguments[0] != typeof (object))
				return false;
			var type = typeArguments[1];
			var requiredContractNames = containerService.Context.RequiredContractNames();
			var hostService = containerService.Context.GetPreviousService();
			Func<object, object> factory =
				arguments =>
				{
					var topService = containerService.Context.GetTopService();
					return topService == hostService
						? container.Create(type, requiredContractNames, arguments,
							containerService.Context).SingleInstance(true)
						: container.Create(type, requiredContractNames, arguments);
				};
			containerService.AddInstance(DelegateCaster.Create(type).Cast(factory));
			containerService.UseAllContracts(requiredContractNames.Length);
			return true;
		}
	}
}