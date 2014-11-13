using System.Linq;
using NUnit.Framework;

namespace SimpleContainer.Tests.GenericsConfiguratorTests
{
	public class CanInjectGenericComponentTest : PreconfiguredContainerTestBase
	{
		public abstract class Restriction
		{
		}

		public class ConcreteRestriction: Restriction
		{
		}

		public class Component<T>
			where T: Restriction
		{
		}

		[Test]
		public void Test()
		{
			Assert.That(container.GetAll<Component<ConcreteRestriction>>().Count(), Is.EqualTo(1));
		}
	}
}