using System;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal static class NestedFactoryCreator
	{
		public static bool TryCreate(ContainerService.Builder builder)
		{
			var factoryType = builder.Type.GetNestedType("Factory");
			if (factoryType == null)
				return false;
			var method = factoryType.GetMethod("Create", Type.EmptyTypes);
			if (method == null)
				return false;
			var factory = builder.Context.container.ResolveCore(new ServiceName(method.DeclaringType), false, null,
				builder.Context);
			var dependency = factory.AsSingleInstanceDependency(null);
			builder.AddDependency(dependency, false);
			if (dependency.Status == ServiceStatus.Ok)
				builder.CreateInstance(method, dependency.Value, new object[0]);
			return true;
		}
	}
}