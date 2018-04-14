using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Infection;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.ConstructionLog
{
	public abstract class ConstructionLogTest : SimpleContainerTestBase
	{
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
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(FormatMessage(@"
A
	parameter -> qq")));
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
				Assert.That(resolved.GetConstructionLog(), Is.EqualTo(FormatMessage(@"
A
	v -> 76
	Func<B>
	() => B
		parameter -> 67")));
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
				Assert.That(resolvedService.GetConstructionLog(), Is.EqualTo(FormatMessage(@"
A
	B - DontUse -> <null>")));
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

			[DontUse]
			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var resolvedService = container.Resolve<A>();
				Assert.That(resolvedService.Single().b, Is.Null);
				Assert.That(resolvedService.GetConstructionLog(), Is.EqualTo(FormatMessage(@"
A
	B - DontUse -> <null>")));
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
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(FormatMessage(@"
A
	parameter -> <null>")));
			}
		}

		public class MergeFailedConstructionLog : ConstructionLogTest
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
				Assert.That(error.Message, Is.EqualTo(FormatMessage(@"
many instances for [IB]
	B1
	B2

!C
	!A
		IB++
			B1
			B2")));
			}
		}

		public class DumpCommentOnlyOnce : ConstructionLogTest
		{
			public class X
			{
				public readonly IA a1;
				public readonly IA a2;

				public X(IA a1, IA a2)
				{
					this.a1 = a1;
					this.a2 = a2;
				}
			}

			public interface IA
			{
			}

			public class A1 : IA
			{
			}

			public class A2 : IA
			{
			}

			public class AConfigurator : IServiceConfigurator<IA>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<IA> builder)
				{
					builder.WithInstanceFilter(a => a.GetType() == typeof (A2));
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Resolve<X>().GetConstructionLog(), Is.EqualTo(FormatMessage(@"
X
	IA - instance filter
		A1
		A2
	IA")));
			}
		}

		public class ConstructionLogForReusedService : ConstructionLogTest
		{
			public class A
			{
			}

			public class B
			{
				public readonly A a1;
				public readonly A a2;
				public readonly A a3;

				public B(A a1, A a2, A a3)
				{
					this.a1 = a1;
					this.a2 = a2;
					this.a3 = a3;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Resolve<B>().GetConstructionLog(), Is.EqualTo(FormatMessage(@"
B
	A
	A
	A")));
			}
		}

		public class DisplayInitializingStatus : ConstructionLogTest
		{
			private static readonly StringBuilder log = new StringBuilder();

			public class A : IInitializable
			{
				public readonly B b;
				public static ManualResetEventSlim goInitialize = new ManualResetEventSlim();

				public A(B b)
				{
					this.b = b;
				}

				public void Initialize()
				{
					goInitialize.Wait();
					log.Append("A.Initialize ");
				}
			}

			public class B: IInitializable
			{
				public static ManualResetEventSlim goInitialize = new ManualResetEventSlim();

				public void Initialize()
				{
					goInitialize.Wait();
					log.Append("B.Initialize ");
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var t = Task.Run(() => container.Get<A>());
				Thread.Sleep(15);
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(FormatMessage(@"
A, initializing ...
	B, initializing ...")));
				Assert.That(log.ToString(), Is.EqualTo(""));
				B.goInitialize.Set();
				Thread.Sleep(5);
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(FormatMessage(@"
A, initializing ...
	B")));
				Assert.That(log.ToString(), Is.EqualTo("B.Initialize "));
				A.goInitialize.Set();
				Thread.Sleep(5);
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(FormatMessage(@"
A
	B")));
				Assert.That(log.ToString(), Is.EqualTo("B.Initialize A.Initialize "));
				t.Wait();
			}
		}
		
		public class DisplayDisposingStatus : ConstructionLogTest
		{
			private static readonly StringBuilder log = new StringBuilder();

			public class A : IDisposable
			{
				public readonly B b;
				public static ManualResetEventSlim go = new ManualResetEventSlim();

				public A(B b)
				{
					this.b = b;
				}

				public void Dispose()
				{
					go.Wait();
					log.Append("A.Dispose ");
				}
			}

			public class B: IDisposable
			{
				public static ManualResetEventSlim go = new ManualResetEventSlim();

				public void Dispose()
				{
					go.Wait();
					log.Append("B.Dispose ");
				}
			}

			protected override void TearDown()
			{
				A.go.Set();
				B.go.Set();
				base.TearDown();
			}

			[Test]
			public void Test()
			{
				var container = Container();
				container.Get<A>();
				var t = Task.Run(() => container.Dispose());
				Thread.Sleep(15);
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(FormatMessage(@"
A, disposing ...
	B")));
				Assert.That(log.ToString(), Is.EqualTo(""));
				A.go.Set();
				Thread.Sleep(5);
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(FormatMessage(@"
A
	B, disposing ...")));
				Assert.That(log.ToString(), Is.EqualTo("A.Dispose "));
				B.go.Set();
				Thread.Sleep(5);
				Assert.Throws<ObjectDisposedException>(() => container.Resolve<A>());
				t.Wait();
			}
		}

		public class CycleSpanningContainerDependency : ConstructionLogTest
		{
			public class A
			{
				public A(IContainer container)
				{
					container.Get<B>();
				}
			}

			public class B
			{
				public readonly A a;

				public B(A a)
				{
					this.a = a;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var exception = Assert.Throws<SimpleContainerException>(() => container.Get<A>());
				Assert.That(exception.InnerException, Is.Null);
				Assert.That(exception.Message, Is.EqualTo(FormatMessage(@"
cyclic dependency for service [A], stack
	A
	B
	A

!A
	IContainer
	!() => B
		!A")));
			}
		}

		public class PrettyLogForBadResolveInDependencyFactoryDelegate : ConstructionLogTest
		{
			[TestContract("c1")]
			public class A
			{
				public readonly string item;

				public A(string item)
				{
					this.item = item;
				}
			}

			public class B
			{
				public string value = "42";
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.BindDependencyFactory<A>("item", c => c.Get<B>("c1").value));
				var exception = Assert.Throws<SimpleContainerException>(() => container.Get<A>());
				Assert.That(exception.Message, Is.EqualTo(FormatMessage(@"
contract [c1] already declared, stack
	A[c1]
	item
	B[c1]

!A
	!item
		!() => B <---------------")));
			}
		}
	}
}