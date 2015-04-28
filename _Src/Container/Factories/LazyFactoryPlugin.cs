using System;
using System.Linq;
using System.Reflection;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation;
using SimpleContainer.Implementation.Hacks;

namespace SimpleContainer.Factories
{
	internal class LazyFactoryPlugin : IFactoryPlugin
	{
		public bool TryInstantiate(Implementation.SimpleContainer container, ContainerService containerService)
		{
			if (!containerService.Type.GetTypeInfo().IsGenericType)
				return false;
			if (containerService.Type.GetGenericTypeDefinition() != typeof (Lazy<>))
				return false;
			var type = containerService.Type.GetGenericArguments()[0];
			var lazyFactoryCtor = typeof (LazyFactory<>).MakeGenericType(type).GetConstructors().Single();
			var lazyFactory = (ILazyFactory) MethodInvoker.Invoke(lazyFactoryCtor, null, new object[] {container});
			containerService.AddInstance(lazyFactory.CreateLazy(), true);
			return true;
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