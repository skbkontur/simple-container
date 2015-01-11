using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleContainer.Interface;

namespace SimpleContainer.Tests.LongRunning
{
	public class CrashOnOneThreadDoNotAffectOtherThreads : SimpleContainerTestBase
	{
		public class A
		{
			public readonly ServiceWithDelay serviceWithDelay;
			public readonly int parameter;

			public A(ServiceWithDelay serviceWithDelay, int parameter)
			{
				this.serviceWithDelay = serviceWithDelay;
				this.parameter = parameter;
			}
		}

		public class ServiceWithDelay
		{
			public ServiceWithDelay()
			{
				Thread.Sleep(100);
			}
		}

		[Test]
		public void Test()
		{
			var container = Container();
			var barrier = new Barrier(2);
			var otherThreadTask = Task.Run(delegate
			{
				barrier.SignalAndWait();
				container.Get<A>();
			});
			var error = Assert.Throws<SimpleContainerException>(() =>
			{
				barrier.SignalAndWait();
				Thread.Sleep(20);
				container.Get<A>();
			});
			Assert.That(error.Message, Is.StringStarting("can't create simple type\r\nA!\r\n\tparameter!"));
			Assert.That(otherThreadTask.Exception.InnerExceptions.Single().Message,
				Is.StringStarting("can't create simple type\r\nA!\r\n\tServiceWithDelay\r\n\tparameter!"));
		}
	}
}