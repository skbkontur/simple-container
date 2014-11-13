using NUnit.Framework;

namespace SimpleContainer.Tests
{
	[TestFixture]
	public class UnitTestBase
	{
		[SetUp]
		protected virtual void SetUp()
		{
		}

		[TearDown]
		protected virtual void TearDown()
		{
		}

		[TestFixtureSetUp]
		public virtual void TestFixtureSetUp()
		{
		}

		[TestFixtureTearDown]
		public virtual void TestFixtureTearDown()
		{
		}
	}
}