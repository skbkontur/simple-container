using System;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal static class LazyCreator
	{
		public static object TryCreate(ContainerService.Builder builder)
		{
			if (!builder.Type.IsGenericType)
				return null;
			if (builder.Type.GetGenericTypeDefinition() != typeof (Lazy<>))
				return null;
			var resultType = builder.Type.GetGenericArguments()[0];
			builder.Context.AnalizeDependenciesOnly = true;
			var containerService = builder.Context.Instantiate(resultType, true, null);
			builder.Context.AnalizeDependenciesOnly = false;
			builder.UnionUsedContracts(containerService);
			var lazyFactoryCtor = typeof (LazyFactory<>).MakeGenericType(resultType).GetConstructors().Single();
			var lazyFactory = (ILazyFactory) lazyFactoryCtor.Compile()(null, new object[] {builder.Context.Container});
			return lazyFactory.CreateLazy();
		}

		private interface ILazyFactory
		{
			object CreateLazy();
		}

		private class LazyFactory<T> : ILazyFactory
		{
			private readonly IContainer container;

			public LazyFactory(IContainer container)
			{
				this.container = container;
			}

			public object CreateLazy()
			{
				return new Lazy<T>(() =>
				{
					var current = ContainerService.Builder.Current;
					if (current == null)
						return container.Get<T>();
					var result = current.Context.Resolve(ServiceName.Parse(typeof (T), false));
					var resultDependency = result.AsSingleInstanceDependency("() => " + result.Type.FormatName());
					current.AddDependency(resultDependency, false);
					if (resultDependency.Status != ServiceStatus.Ok)
						throw new ServiceCouldNotBeCreatedException();
					return (T) resultDependency.Value;
				});
			}
		}
	}
}