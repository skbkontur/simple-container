using System;
using SimpleContainer.Implementation;

namespace SimpleContainer.Factories
{
	internal static class FactoryCreator
	{
		public static Func<object, object> CreateFactory(Type type, Implementation.SimpleContainer container,
			ContainerService containerService)
		{
			var declaredContractNames = containerService.Context.DeclaredContractNames();
			var hostService = containerService.Context.GetPreviousService();
			containerService.UseAllDeclaredContracts();
			return delegate(object arguments)
			{
				ContainerService result;
				if (hostService != containerService.Context.GetTopService())
				{
					result = container.Create(type, declaredContractNames, arguments, null);
					container.Run(result, null);
				}
				else
				{
					result = container.Create(type, declaredContractNames, arguments, containerService.Context);
					containerService.AddDependency(result);
				}
				return result.SingleInstance(true);
			};
		}
	}
}