using System;
using NUnit.Framework;

namespace SimpleContainer.Tests.FactoryConfiguratorTests
{
	public class ServicesUsedByServicesCreatedByFactoriesAreSingletons : SimpleContainerTestBase
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
			var factory = Container().Get<ServiceC>().factory;
			Assert.That(factory.Invoke(new {someValue = "x"}).serviceA,
				Is.SameAs(factory.Invoke(new {someValue = "y"}).serviceA));
		}
	}
}