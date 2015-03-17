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
				var resultDependency = AsDependency(result, factoryService, hostService);
				factoryService.AddDependency(resultDependency);
				if (factoryService.status != ServiceStatus.Ok)
					throw new ServiceCouldNotBeCreatedException();
				return resultDependency.Value;
			};
		}

		private static ServiceDependency AsDependency(ContainerService service,
			ContainerService factoryService, ContainerService hostService)
		{
			if (service.status != ServiceStatus.Ok)
				return ServiceDependency.ServiceError(service);
			if (service.Instances.Count == 0)
				return ServiceDependency.NotResolved(service);
			if (service.Instances.Count > 1)
			{
				var factoryName = hostService.GetDependency(factoryService).Name;
				return ServiceDependency.Error(factoryName + ":" + service.Type.FormatName(),
					service.FormatManyImplementationsMessage());
			}
			return ServiceDependency.Service(service, service.Instances[0]);
		}
	}
}