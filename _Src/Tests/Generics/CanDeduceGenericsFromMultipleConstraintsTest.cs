using System.Linq;
using NUnit.Framework;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.Generics
{
	public class CanDeduceGenericsFromMultipleConstraintsTest : SimpleContainerTestBase
	{
		public interface IConstraintA
		{
		}

		public interface IConstraintB
		{
		}

		public class MeetsConstraints : IConstraintA, IConstraintB
		{
		}

		public class MeetsOnlyOneConstraint : IConstraintA
		{
		}

		public interface IInterface
		{
		}

		public class Implementation<TParameter> : IInterface
			where TParameter : IConstraintA, IConstraintB
		{
		}

		[Test]
		public void Test()
		{
			var all = Container().GetAll<IInterface>().ToArray();
			Assert.That(all.Length, Is.EqualTo(1));
			Assert.That(all.Single().GetType(), Is.EqualTo(typeof (Implementation<MeetsConstraints>)));
		}
	}
}