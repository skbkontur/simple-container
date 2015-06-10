using System;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation;

namespace SimpleContainer.Factories
{
	internal static class LazyCreator
	{
		public static object TryCreate(ContainerService.Builder builder)
		{
			if (!builder.Type.IsGenericType)
				return null;
			if (builder.Type.GetGenericTypeDefinition() != typeof (Lazy<>))
				return null;
			var type = builder.Type.GetGenericArguments()[0];
			var lazyFactoryCtor = typeof (LazyFactory<>).MakeGenericType(type).GetConstructors().Single();
			var lazyFactory =
				(ILazyFactory) MethodInvoker.Invoke(lazyFactoryCtor, null, new object[] {builder.Container});
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