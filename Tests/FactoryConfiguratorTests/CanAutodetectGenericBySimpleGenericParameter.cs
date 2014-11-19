using System;
using NUnit.Framework;
using SimpleContainer.Tests.GenericsConfiguratorTests;

namespace SimpleContainer.Tests.FactoryConfiguratorTests
{
	public class CanAutodetectGenericBySimpleGenericParameter : SimpleContainerTestBase
	{
		public class SomeOuterService
		{
			private readonly Func<object, ISomeService> factory;

			public SomeOuterService(Func<object, ISomeService> factory)
			{
				this.factory = factory;
			}

			public ISomeService Create(object item)
			{
				return factory(new { item });
			}

			public class SomeService<T> : ISomeService
			{
				public T Item { get; private set; }

				public SomeService(T item)
				{
					Item = item;
				}
			}
		}

		public interface ISomeService
		{
		}
		


		[Test]
		public void Test()
		{
			var someOuterService = Container().Get<SomeOuterService>();
			var someService = someOuterService.Create(23);
			var typedSomeService = someService as SomeOuterService.SomeService<int>;
			Assert.That(typedSomeService, Is.Not.Null);
			Assert.That(typedSomeService.Item, Is.EqualTo(23));
		}
	}
}
