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
			var contract = containerService.context.ContractName;
			Func<object, object> factory = arguments => container.Create(type, contract, arguments);
			containerService.instances.Add(DelegateCaster.Create(type).Cast(factory));
			containerService.contractUsed = true;
			return true;
		}
	}
}