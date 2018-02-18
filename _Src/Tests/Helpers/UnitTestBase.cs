using NUnit.Framework;

namespace SimpleContainer.Tests.Helpers
{
	[TestFixture]
	public abstract class UnitTestBase
	{
		[SetUp]
		protected virtual void SetUp()
		{
		}

		[TearDown]
		protected virtual void TearDown()
		{
		}

		[OneTimeSetUp]
		public virtual void TestFixtureSetUp()
		{
		}

		[OneTimeTearDown]
		public virtual void TestFixtureTearDown()
		{
		}
	}
}