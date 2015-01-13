using System.Linq;
using NUnit.Framework;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.GenericsConfiguratorTests
{
	public class CanUseGenericAndNonGenericImplementationsOfOneInterface : SimpleContainerTestBase
	{
		public abstract class Restriction
		{
		}

		public class ConcreteRestriction : Restriction
		{
		}

		public interface IMyInterface
		{
		}

		public class GenericComponent<T> : IMyInterface
			where T : Restriction
		{
		}

		public class NonGenericComponent : IMyInterface
		{
		}

		[Test]
		public void Test()
		{
			Assert.That(Container().GetAll<IMyInterface>().Count(), Is.EqualTo(2));
		}
	}
}