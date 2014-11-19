using System;
using SimpleContainer.Implementation;

namespace SimpleContainer.Factories
{
	public class FactoryWithArgumentsPlugin : IFactoryPlugin
	{
		public bool TryInstantiate(IServiceFactory serviceFactory, ContainerService containerService)
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
			var contract = containerService.context.Contract;
			Func<object, object> factory = arguments => serviceFactory.Create(type, contract, arguments);
			containerService.instances.Add(DelegateCaster.Create(type).Cast(factory));
			containerService.contractUsed = true;
			return true;
		}
	}
}