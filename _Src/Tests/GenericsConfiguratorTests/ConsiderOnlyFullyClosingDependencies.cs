using NUnit.Framework;

namespace SimpleContainer.Tests.GenericsConfiguratorTests
{
	public class ConsiderOnlyFullyClosingDependencies : SimpleContainerTestBase
	{
		public interface IReference
		{
		}

		public class Reference<T1, T2> : IReference
		{
			public readonly IReferenceSource<T1> source;

			public Reference(IReferenceSource<T1> source)
			{
				this.source = source;
			}
		}

		public interface IReferenceSource<T1>
		{
		}

		public class ReferenceSource : IReferenceSource<int>
		{
		}

		[Test]
		public void Test()
		{
			Assert.That(Container().GetAll<IReference>(), Is.Empty);
		}
	}
}