using System;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation;
using SimpleContainer.Interface;

namespace SimpleContainer.Factories
{
	internal static class FactoryCreator
	{
		public static Func<Type, object, object> CreateFactory(ContainerService.Builder builder)
		{
			var declaredContractNames = builder.DeclaredContracts;
			var hostService = builder.Context.GetPreviousService();
			builder.UseAllDeclaredContracts();
			return delegate(Type type, object arguments)
			{
				if (hostService == null || hostService != builder.Context.GetTopService())
				{
					var resolvedService = builder.Container.Create(type, declaredContractNames, arguments);
					resolvedService.Run();
					return resolvedService.Single();
				}
				var result = builder.Container.Create(type, declaredContractNames, arguments, builder.Context);
				var resultDependency = result.AsSingleInstanceDependency("() => " + result.Type.FormatName());
				hostService.AddDependency(resultDependency, false);
				if (hostService.Status != ServiceStatus.Ok)
					throw new ServiceCouldNotBeCreatedException();
				return resultDependency.Value;
			};
		}
	}
}