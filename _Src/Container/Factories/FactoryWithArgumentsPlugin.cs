using System;
using SimpleContainer.Implementation;

namespace SimpleContainer.Factories
{
	internal class FactoryWithArgumentsPlugin : IFactoryPlugin
	{
		public bool TryInstantiate(ContainerService.Builder builder)
		{
			var funcType = builder.Type;
			if (!funcType.IsGenericType)
				return false;
			if (funcType.GetGenericTypeDefinition() != typeof (Func<,>))
				return false;
			var typeArguments = funcType.GetGenericArguments();
			if (typeArguments[0] != typeof (object))
				return false;
			var type = typeArguments[1];
			var baseFactory = FactoryCreator.CreateFactory(builder);
			Func<object, object> factory = o => baseFactory(type, o);
			builder.AddInstance(DelegateCaster.Create(type).Cast(factory), true);
			return true;
		}
	}
}