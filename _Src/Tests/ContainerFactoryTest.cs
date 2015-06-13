using NUnit.Framework;

namespace SimpleContainer.Tests
{
	public abstract class ContainerFactoryTest : UnitTestBase
	{
		public class CanSkipAssemblyFilter : ContainerFactoryTest
		{
			public class A
			{
			}

			[Test]
			public void Test()
			{
				var container = new ContainerFactory()
					.WithTypesFromDefaultBinDirectory(false)
					.Build();
				Assert.That(container.Get<A>(), Is.Not.Null);
			}
		}
	}
}