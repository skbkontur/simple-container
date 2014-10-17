using System;

namespace SimpleContainer.Factories
{
	public class FactoryWithArgumentsPlugin: FactoryCreatorBase
	{
		private readonly IServiceFactory serviceFactory;

		public FactoryWithArgumentsPlugin(IServiceFactory serviceFactory)
		{
			this.serviceFactory = serviceFactory;
		}

		public override bool TryInstantiate(ContainerService containerService)
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
			var contextKey = containerService.context.ContextKey;
			Func<object, object> factory = arguments => serviceFactory.Create(type, contextKey, arguments);
			containerService.instances.Add(GetCaster(type).CastToTyped(factory));
			containerService.contractUsed = true;
			return true;
		}
	}
}