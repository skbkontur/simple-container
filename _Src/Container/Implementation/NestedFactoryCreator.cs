using System;
using System.Reflection;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	internal static class NestedFactoryCreator
	{
		public static bool TryCreate(ContainerService.Builder builder)
		{
			var factoryType = builder.Type.GetNestedType("Factory", BindingFlags.Public);
			if (factoryType == null)
				return false;
			var method = factoryType.GetMethod("Create", Type.EmptyTypes);
			if (method == null)
				return false;
			var factory = builder.Context.Container.Resolve(method.DeclaringType, InternalHelpers.emptyStrings, false);
			if (factory.IsOk())
				builder.CreateInstanceBy(CallTarget.M(method, factory.Single(), new object[0]), true);
			return true;
		}
	}
}