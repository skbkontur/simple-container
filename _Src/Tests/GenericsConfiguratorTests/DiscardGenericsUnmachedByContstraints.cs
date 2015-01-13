using System.Linq;
using NUnit.Framework;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.GenericsConfiguratorTests
{
	public class DiscardGenericsUnmachedByContstraints : SimpleContainerTestBase
	{
		public interface IMyWrapper
		{
		}

		public class MyWrapper<T> : IMyWrapper
			where T : Constraint
		{
			public readonly IMyHandler<T> handler;

			public MyWrapper(IMyHandler<T> handler)
			{
				this.handler = handler;
			}
		}

		public interface IMyHandler<T>
		{
		}

		public class Constraint
		{
		}

		public class TestData
		{
		}

		public class MyService : IMyHandler<TestData>
		{
		}

		public class MyService2 : IMyHandler<Constraint>
		{
		}

		[Test]
		public void Test()
		{
			Assert.That(Container().GetAll<IMyWrapper>().Cast<MyWrapper<Constraint>>().Single().handler,
				Is.InstanceOf<MyService2>());
		}
	}
}