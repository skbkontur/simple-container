using NUnit.Framework;

namespace SimpleContainer.Tests
{
	public abstract class ConstructionLogTest : SimpleContainerTestBase
	{
		public class DumpUsedSimpleTypesInConstructionLog : BasicTest
		{
			public class A
			{
				public readonly int parameter;

				public A(int parameter)
				{
					this.parameter = parameter;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.BindDependency<A>("parameter", 78));
				container.Get<A>();
				const string expectedConstructionLog = "A\r\n\tparameter -> 78";
				Assert.That(container.GetConstructionLog(typeof (A)), Is.EqualTo(expectedConstructionLog));
			}
		}

		public class DumpSimpleTypesFromFactoryInConstructionLog : BasicTest
		{
			public class A
			{
				public readonly string parameter;

				public A(string parameter)
				{
					this.parameter = parameter;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.BindDependencyFactory<A>("parameter", _ => "qq"));
				container.Get<A>();
				const string expectedConstructionLog = "A\r\n\tparameter -> qq";
				Assert.That(container.GetConstructionLog(typeof (A)), Is.EqualTo(expectedConstructionLog));
			}
		}

		public class ExplicitNull : BasicTest
		{
			public class A
			{
				public readonly string parameter;

				public A(string parameter = null)
				{
					this.parameter = parameter;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				container.Get<A>();
				const string expectedConstructionLog = "A\r\n\tparameter -> <null>";
				Assert.That(container.GetConstructionLog(typeof (A)), Is.EqualTo(expectedConstructionLog));
			}
		}
	}
}