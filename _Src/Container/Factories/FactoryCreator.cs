using System;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation;
using SimpleContainer.Interface;

namespace SimpleContainer.Factories
{
	internal static class FactoryCreator
	{
		public static Func<object, object> CreateFactory(Type type, Implementation.SimpleContainer container,
			ContainerService factoryService)
		{
			var declaredContractNames = factoryService.Context.DeclaredContractNames();
			var hostService = factoryService.Context.GetPreviousService();
			factoryService.UseAllDeclaredContracts();
			return delegate(object arguments)
			{
				if (hostService != factoryService.Context.GetTopService())
				{
					var resolvedService = container.Create(type, declaredContractNames, arguments);
					resolvedService.Run();
					return resolvedService.Single();
				}
				var result = container.Create(type, declaredContractNames, arguments, factoryService.Context);
				var factoryName = hostService.GetDependency(factoryService).Name;
				var resultDependency = result.AsSingleInstanceDependency(factoryName + ":" + result.Type.FormatName());
				factoryService.AddDependency(resultDependency);
				if (factoryService.status != ServiceStatus.Ok)
					throw new ServiceCouldNotBeCreatedException();
				return resultDependency.Value;
			};
		}
	}
}