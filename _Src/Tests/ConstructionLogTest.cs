using System;
using NUnit.Framework;
using SimpleContainer.Infection;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class ConstructionLogTest : SimpleContainerTestBase
	{
		public class DumpUsedSimpleTypesInConstructionLog : ConstructionLogTest
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
				const string expectedConstructionLog = "A\r\n\tparameter -> 78";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expectedConstructionLog));
			}
		}

		public class DumpSimpleTypesFromFactoryInConstructionLog : ConstructionLogTest
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
				const string expectedConstructionLog = "A\r\n\tparameter -> qq";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expectedConstructionLog));
			}
		}

		public class DumpTimeSpansAsSimpleTypes : ConstructionLogTest
		{
			public class A
			{
				public readonly TimeSpan parameter;

				public A(TimeSpan parameter)
				{
					this.parameter = parameter;
				}
			}

			[Test]
			public void Test()
			{
				var ts = TimeSpan.FromSeconds(54).Add(TimeSpan.FromMilliseconds(17));
				var container = Container(b => b.BindDependency<A>("parameter", ts));;
				const string expectedConstructionLog = "A\r\n\tparameter -> 00:00:54.0170000";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expectedConstructionLog));
			}
		}

		public class DumpNullablesAsSimpleTypes : ConstructionLogTest
		{
			public class A
			{
				public readonly int? value;

				public A(int? value)
				{
					this.value = value;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.BindDependency<A>("value", 21));
				const string expectedConstructionLog = "A\r\n\tvalue -> 21";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expectedConstructionLog));
			}
		}

		public class DumpEnumsAsSimpleTypes : ConstructionLogTest
		{
			public enum MyEnum
			{
				Val1,
				Val2
			}

			public class A
			{
				public readonly MyEnum value;

				public A(MyEnum value)
				{
					this.value = value;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.BindDependency<A>("value", MyEnum.Val2));
				const string expectedConstructionLog = "A\r\n\tvalue -> Val2";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expectedConstructionLog));
			}
		}

		public class CommentExplicitDontUse : ConstructionLogTest
		{
			public class A
			{
				public readonly B b;

				public A(B b = null)
				{
					this.b = b;
				}
			}

			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.DontUse<B>());
				var resolvedService = container.Resolve<A>();
				Assert.That(resolvedService.Single().b, Is.Null);
				Assert.That(resolvedService.GetConstructionLog(), Is.EqualTo("A\r\n\tB! - DontUse"));
			}
		}

		public class CommentIgnoreImplementation : ConstructionLogTest
		{
			public class A
			{
				public readonly B b;

				public A(B b = null)
				{
					this.b = b;
				}
			}

			[IgnoredImplementation]
			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var resolvedService = container.Resolve<A>();
				Assert.That(resolvedService.Single().b, Is.Null);
				Assert.That(resolvedService.GetConstructionLog(), Is.EqualTo("A\r\n\tB! - IgnoredImplementation"));
			}
		}

		public class ExplicitNull : ConstructionLogTest
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
				const string expectedConstructionLog = "A\r\n\tparameter -> <null>";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expectedConstructionLog));
			}
		}
	}
}