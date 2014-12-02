using System;
using SimpleContainer.Implementation;

namespace SimpleContainer.Factories
{
	internal class FactoryWithArgumentsPlugin : IFactoryPlugin
	{
		public bool TryInstantiate(IContainer container, ContainerService containerService)
		{
			var funcType = containerService.type;
			if (!funcType.IsGenericType)
				return false;
			if (funcType.GetGenericTypeDefinition() != typeof (Func<,>))
				return false;
			var typeArguments = funcType.GetGenericArguments();
			if (typeArguments[0] != typeof (object))
				return false;
			var type = typeArguments[1];
			var requiredContractNames = containerService.context.RequiredContractNames();
			Func<object, object> factory = arguments => container.Create(type, requiredContractNames, arguments);
			containerService.AddInstance(DelegateCaster.Create(type).Cast(factory));
			containerService.UseAllContracts(requiredContractNames.Length);
			return true;
		}
	}
}