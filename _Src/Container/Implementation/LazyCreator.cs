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
			var oldValue = builder.Context.AnalizeDependenciesOnly;
			builder.Context.AnalizeDependenciesOnly = true;
			var containerService = builder.Context.Container.ResolveCore(new ServiceName(resultType), true, null, builder.Context);
			builder.Context.AnalizeDependenciesOnly = oldValue;
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
				return new Lazy<T>(() => container.Get<T>());
			}
		}
	}
}