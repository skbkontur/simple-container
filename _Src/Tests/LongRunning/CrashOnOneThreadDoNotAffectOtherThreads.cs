using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

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
			var expected = TestHelpers.FormatMessage(@"
parameter [parameter] of service [A] is not configured

!A
	ServiceWithDelay
	!parameter <---------------");
			Assert.That(error.Message, Is.EqualTo(expected));
			var otherTaskException = Assert.Throws<AggregateException>(otherThreadTask.Wait);
			Assert.That(otherTaskException.InnerExceptions.Single().Message, Is.EqualTo(expected));
		}
	}
}