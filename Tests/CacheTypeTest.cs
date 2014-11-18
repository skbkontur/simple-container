using NUnit.Framework;

namespace SimpleContainer.Tests
{
	public abstract class CacheTypeTest : SimpleContainerTestBase
	{
		public class SimpleNoCache : CacheTypeTest
		{
			public class ClassA
			{
			}

			public interface IInterface
			{
			}

			public class Impl : IInterface
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Create<ClassA>(), Is.Not.SameAs(container.Create<ClassA>()));
				Assert.That(container.Create<ClassA>(), Is.Not.SameAs(container.Get<ClassA>()));
				Assert.That(container.Create<IInterface>(), Is.Not.SameAs(container.Get<IInterface>()));
				Assert.That(container.Create<IInterface>(), Is.Not.SameAs(container.Create<IInterface>()));
				Assert.That(container.Get<Impl>(), Is.SameAs(container.Get<Impl>()));
			}
		}

		//public class HostCachedServiceCannotReferenceNoCache : CacheTypeTest
		//{
		//	[Cache(CacheLevel.Host)]
		//	public class Service1
		//	{
		//		public Service1(Service2 service2)
		//		{
		//		}
		//	}
			
		//	[Cache(CacheLevel.None)]
		//	public class Service2
		//	{
		//	}

		//	[Test]
		//	public void Test()
		//	{
		//		var container = Container();
		//		var error = Assert.Throws<SimpleContainerException>(() => container.Get<Service1>());
		//		Assert.That(error.Message, Is.EqualTo("dependency [service2] of type [Service2] cacheLevel [None] is incompatibe with cacheLevel [Host] of [Service1]\r\nService1! - <---------------\r\n\tService2"));
		//	}
		//}
	}
}