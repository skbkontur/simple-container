using System;
using NUnit.Framework;

namespace SimpleContainer.Tests.FactoryConfiguratorTests
{
	public class SimpleFactoryConfiguratorTest: FactoryConfigurationTestBase
	{
		public class ServiceA
		{
		}

		public class ServiceB
		{
			public readonly ServiceA serviceA;
			public readonly string someValue;

			public ServiceB(ServiceA serviceA, string someValue)
			{
				this.serviceA = serviceA;
				this.someValue = someValue;
			}
		}

		public class ServiceC
		{
			public readonly Func<object, ServiceB> factory;

			public ServiceC(Func<object, ServiceB> factory)
			{
				this.factory = factory;
			}
		}

		[Test]
		public void Test()
		{
			var service = container.Get<ServiceC>().factory.Invoke(new { someValue = "x" });
			Assert.That(service.serviceA, Is.Not.Null);
			Assert.That(service.someValue, Is.EquivalentTo("x"));
		}
	}
}