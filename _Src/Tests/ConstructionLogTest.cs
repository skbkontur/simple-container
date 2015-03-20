using System;
using System.Threading;
using NUnit.Framework;
using SimpleContainer.Infection;
using SimpleContainer.Interface;
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
				var container = Container(b => b.BindDependency<A>("parameter", ts));
				const string expectedConstructionLog = "A\r\n\tparameter -> 00:00:54.0170000";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expectedConstructionLog));
			}
		}
		
		public class DumpValuesOnlyForSimpleTypes : ConstructionLogTest
		{
			public class A
			{
				public readonly CancellationToken token;

				public A(CancellationToken token)
				{
					this.token = token;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.BindDependency<A>("token", CancellationToken.None));
				const string expectedConstructionLog = "A\r\n\tCancellationToken const";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expectedConstructionLog));
			}
		}
		
		public class DumpBooleansInLowercase : ConstructionLogTest
		{
			public class A
			{
				public readonly bool someBool;

				public A(bool someBool)
				{
					this.someBool = someBool;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.BindDependency<A>("someBool", true));
				const string expectedConstructionLog = "A\r\n\tsomeBool -> true";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expectedConstructionLog));
			}
		}

		public class PrintFuncArgumentNameWhenInvokedFromCtor : ConstructionLogTest
		{
			public class A
			{
				public readonly int v;
				public readonly B b;

				public A(int v, Func<B> myFactory)
				{
					this.v = v;
					b = myFactory();
				}
			}

			public class B
			{
				public readonly int parameter;

				public B(int parameter)
				{
					this.parameter = parameter;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(c => c.BindDependency<A>("v", 76).BindDependency<B>("parameter", 67));
				var resolved = container.Resolve<A>();
				Assert.That(resolved.GetConstructionLog(), Is.EqualTo("A\r\n\tv -> 76\r\n\tFunc<B>\r\n\t() => B\r\n\t\tparameter -> 67"));
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
				Assert.That(resolvedService.GetConstructionLog(), Is.EqualTo("A\r\n\tB - DontUse = <null>"));
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
				Assert.That(resolvedService.GetConstructionLog(), Is.EqualTo("A\r\n\tB - IgnoredImplementation = <null>"));
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

		public class MergeFailedConstructionLog : BasicTest
		{
			public class A
			{
				public readonly IB b;

				public A(IB b)
				{
					this.b = b;
				}
			}

			public interface IB
			{
			}

			public class B1 : IB
			{
			}

			public class B2 : IB
			{
			}

			public class C
			{
				public readonly A a;

				public C(A a)
				{
					this.a = a;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.Throws<SimpleContainerException>(() => container.Get<A>());
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<C>());
				const string expectedMessage =
					"many implementations for [IB]\r\n\tB1\r\n\tB2\r\n\r\n!C\r\n\t!A\r\n\t\tIB++\r\n\t\t\tB1\r\n\t\t\tB2";
				Assert.That(error.Message, Is.EqualTo(expectedMessage));
			}
		}
	}
}