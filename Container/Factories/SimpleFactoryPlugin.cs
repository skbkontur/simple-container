using System;

namespace SimpleContainer.Factories
{
	public class SimpleFactoryPlugin: FactoryCreatorBase
	{
		private readonly IServiceFactory serviceFactory;

		public SimpleFactoryPlugin(IServiceFactory serviceFactory)
		{
			this.serviceFactory = serviceFactory;
		}

		public override bool TryInstantiate(ContainerService containerService)
		{
			if (!containerService.type.IsGenericType)
				return false;
			if (containerService.type.GetGenericTypeDefinition() != typeof (Func<>))
				return false;
			var type = containerService.type.GetGenericArguments()[0];
			var contract = containerService.context.ContextKey;
			Func<object> factory = () => serviceFactory.Create(type, contract, null);
			containerService.instances.Add(GetCaster(type).CastToTyped(factory));
			containerService.contractUsed = true;
			return true;
		}
	}
}