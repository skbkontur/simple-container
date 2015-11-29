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
			var oldValue = builder.Context.analizeDependenciesOnly;
			builder.Context.analizeDependenciesOnly = true;
			var containerService = builder.Context.container.ResolveCore(new ServiceName(resultType), true, null, builder.Context);
			builder.Context.analizeDependenciesOnly = oldValue;
			builder.UnionUsedContracts(containerService);
			var lazyFactoryCtor = typeof (LazyFactory<>).MakeGenericType(resultType).GetConstructors().Single();
			var lazyFactory = (ILazyFactory) lazyFactoryCtor.Compile()(null, new object[] {builder.Context.container});
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
					var name = ServiceName.Parse(typeof (T).UnwrapEnumerable(), false, null, null);
					var result = current.Context.container.ResolveCore(name, false, null, current.Context);
					var resultDependency = result.AsImplicitDependency(current.Context.container.containerContext,
						name.Type != typeof (T));
					current.AddDependency(resultDependency, false);
					if (resultDependency.Status != ServiceStatus.Ok)
						throw new ServiceCouldNotBeCreatedException();
					return (T) resultDependency.Value;
				});
			}
		}
	}
}