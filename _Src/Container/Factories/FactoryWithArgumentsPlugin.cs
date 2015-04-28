using System;
using System.Reflection;
using SimpleContainer.Implementation;
using SimpleContainer.Implementation.Hacks;

namespace SimpleContainer.Factories
{
	internal class FactoryWithArgumentsPlugin : IFactoryPlugin
	{
		public bool TryInstantiate(Implementation.SimpleContainer container, ContainerService containerService)
		{
			var funcType = containerService.Type;
			if (!funcType.GetTypeInfo().IsGenericType)
				return false;
			if (funcType.GetGenericTypeDefinition() != typeof (Func<,>))
				return false;
			var typeArguments = funcType.GetGenericArguments();
			if (typeArguments[0] != typeof (object))
				return false;
			var type = typeArguments[1];
			var factory = FactoryCreator.CreateFactory(typeArguments[1], container, containerService);
			containerService.AddInstance(DelegateCaster.Create(type).Cast(factory), true);
			return true;
		}
	}
}