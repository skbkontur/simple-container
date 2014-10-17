using NUnit.Framework;

namespace SimpleContainer.Tests.GenericsConfiguratorTests
{
	public class VerifyConstraintsForDependencies: GenericConfigurationTestBase
	{
		public interface IMyWrapper
		{
		}

		public class MyWrapper<T>: IMyWrapper
			where T: Constraint
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

		public class MyService: IMyHandler<TestData>
		{
		}

		[Test]
		public void Test()
		{
			Assert.That(container.GetAll<IMyWrapper>(), Is.Empty);
		}
	}
}