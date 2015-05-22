using System;
using SimpleContainer.Implementation;

namespace SimpleContainer.Factories
{
	internal class SimpleFactoryPlugin : IFactoryPlugin
	{
		public bool TryInstantiate(Implementation.SimpleContainer container, ContainerService.Builder builder)
		{
			if (!builder.Type.IsGenericType)
				return false;
			if (builder.Type.GetGenericTypeDefinition() != typeof (Func<>))
				return false;
			var type = builder.Type.GetGenericArguments()[0];
			var factoryWithArguments = FactoryCreator.CreateFactory(type, container, builder);
			Func<object> factory = () => factoryWithArguments(null);
			builder.AddInstance(DelegateCaster.Create(type).Cast(factory), true);
			return true;
		}
	}
}