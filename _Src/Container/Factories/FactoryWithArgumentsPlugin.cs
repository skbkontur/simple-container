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
					if (topService != hostService) 
						return container.Create(type, requiredContractNames, arguments);
					var dependency = container.Create(type, requiredContractNames, arguments, containerService.Context);
					containerService.AddDependency(dependency);
					return dependency.SingleInstance(true);
				};
			containerService.AddInstance(DelegateCaster.Create(type).Cast(factory));
			containerService.UseAllRequiredContracts();
			return true;
		}
	}
}