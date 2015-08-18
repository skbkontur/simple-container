using System.Linq;
using NUnit.Framework;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.Generics
{
	public class CanInjectGenericComponentTest : SimpleContainerTestBase
	{
		public abstract class Restriction
		{
		}

		public class ConcreteRestriction : Restriction
		{
		}

		public class Component<T>
			where T : Restriction
		{
		}

		[Test]
		public void Test()
		{
			Assert.That(Container().GetAll<Component<ConcreteRestriction>>().Count(), Is.EqualTo(1));
		}
	}
}