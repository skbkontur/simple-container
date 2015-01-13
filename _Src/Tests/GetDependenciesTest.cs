using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimpleContainer.Infection;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class GetDependenciesTest : SimpleContainerTestBase
	{
		public class NoConstructors_NoDependencies : GetDependenciesTest
		{
			public class A
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.GetDependencies(typeof (A)), Is.Empty);
			}
		}

		public class Simple : GetDependenciesTest
		{
			public class A
			{
				public A(B b)
				{
				}
			}

			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.GetDependencies(typeof (A)), Is.EqualTo(new[] {typeof (B)}));
			}
		}

		public class GetDependenciesForInjections : GetDependenciesTest
		{
			public class A
			{
#pragma warning disable 169
				[Inject] private B b;
#pragma warning restore 169
			}

			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.GetDependencies(typeof (A)), Is.EqualTo(new[] {typeof (B)}));
			}
		}

		public class GetDependenciesForInjectionsInlineEnumerables : GetDependenciesTest
		{
			public class A
			{
#pragma warning disable 169
				[Inject] private IEnumerable<IIntf> b;
#pragma warning restore 169
			}

			public interface IIntf
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.GetDependencies(typeof (A)), Is.EqualTo(new[] {typeof (IIntf)}));
			}
		}

		public class DependencyValues : GetDependenciesTest
		{
			public class A
			{
				public A(B b)
				{
				}
			}

			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.GetDependencyValues(typeof (A)), Is.EqualTo(new object[] {container.Get<B>()}));
			}
		}

		public class Recursive : GetDependenciesTest
		{
			public class A
			{
#pragma warning disable 169
				[Inject] private B b;
#pragma warning restore 169
			}

			public class B
			{
				public B(C c)
				{
				}
			}

			public class C
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.GetDependenciesRecursive(typeof (A)), Is.EquivalentTo(new[] {typeof (B), typeof (C)}));
			}
		}

		public class GetDependencyValuesRecursive : GetDependenciesTest
		{
			public class A
			{
#pragma warning disable 169
				[Inject] private B b;
#pragma warning restore 169
			}

			public class B
			{
				public B(C c)
				{
				}
			}

			public class C
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.GetDependencyValuesRecursive(typeof (A)),
					Is.EquivalentTo(new object[] {container.Get<B>(), container.Get<C>()}));
			}
		}

		public class Uniqueness : GetDependenciesTest
		{
			public class A
			{
				public A(B b, C c)
				{
				}
			}

			public class B
			{
				public B(C c)
				{
				}
			}

			public class C
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.GetDependenciesRecursive(typeof (A)), Is.EquivalentTo(new[] {typeof (B), typeof (C)}));
			}
		}

		public class ManyDependenciesInOneClass : GetDependenciesTest
		{
			public class A
			{
				public A(B b, C c)
				{
				}
			}

			public class B
			{
			}

			public class C
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.GetDependenciesRecursive(typeof (A)), Is.EquivalentTo(new[] {typeof (B), typeof (C)}));
			}
		}

		public class InterfacesCannotContainInjections : GetDependenciesTest
		{
			public class Class
			{
				public Class(IA a)
				{
				}
			}

			public interface IA
			{
			}

			public class A : IA
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.GetDependencyValuesRecursive(typeof (Class)), Is.EqualTo(new object[] {container.Get<A>()}));
			}
		}

		public class SkipFuncs : GetDependenciesTest
		{
			public class Class
			{
				public Class(A a, Func<B> getB)
				{
				}
			}

			public class B
			{
			}

			public class A
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.GetDependencyValuesRecursive(typeof (Class)), Is.EqualTo(new object[] {container.Get<A>()}));
			}
		}

		public class SkipSimpleTypes : GetDependenciesTest
		{
			public class Class
			{
				public Class(B b, int a, string[] strs)
				{
				}
			}

			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.GetDependencyValuesRecursive(typeof (Class)), Is.EqualTo(new object[] {container.Get<B>()}));
			}
		}

		public class SkipExplicitlyConfiguredDependencyValues : GetDependenciesTest
		{
			public class X
			{
				public readonly Y y;

				public X(Y y)
				{
					this.y = y;
				}
			}

			public class Y
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(x => x.BindDependency<X>("y", new Y()));
				Assert.That(container.GetDependencies(typeof (X)), Is.Empty);
			}
		}

		public class SkipExplicitlyConfiguredServices : GetDependenciesTest
		{
			public class X
			{
				public readonly Y y;

				public X(Y y)
				{
					this.y = y;
				}
			}

			public class Y
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(x => x.Bind<Y>(new Y()));
				Assert.That(container.GetDependencies(typeof (X)), Is.Empty);
			}
		}

		public class SkipDependenciesWithFactories : GetDependenciesTest
		{
			public class X
			{
				public readonly Y y;

				public X(Y y)
				{
					this.y = y;
				}
			}

			public class Y
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(x => x.BindDependencyFactory<X>("y", _ => new Y()));
				Assert.That(container.GetDependencies(typeof (X)), Is.Empty);
			}
		}

		public class Enumerable : GetDependenciesTest
		{
			public class Class
			{
				public Class(IEnumerable<IB> b)
				{
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

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.GetDependencyValuesRecursive(typeof (Class)),
					Is.EquivalentTo(new object[] {container.Get<B1>(), container.Get<B2>()}));
			}
		}
	}
}