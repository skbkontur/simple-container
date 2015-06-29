using NUnit.Framework;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.Generics
{
	public class VerifyConstraintsForDependencies : SimpleContainerTestBase
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

		[Test]
		public void Test()
		{
			Assert.That(Container().GetAll<IMyWrapper>(), Is.Empty);
		}
	}
}