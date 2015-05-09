using System.Linq;
using NUnit.Framework;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class NotConfiguredGenericsTest : SimpleContainerTestBase
	{
		public class Simple : NotConfiguredGenericsTest
		{
			public class Generic<T>
			{
			}

			public class GenericClient
			{
				public readonly Generic<A> generic;

				public GenericClient(Generic<A> generic)
				{
					this.generic = generic;
				}
			}

			public class A
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<GenericClient>().generic, Is.Not.Null);
			}
		}

		public class CanCloseImplementationByInterface : NotConfiguredGenericsTest
		{
			public interface IA<T>
			{
			}

			public class A<T> : IA<T>
			{
			}

			public class B
			{
				public readonly IA<int> a;

				public B(IA<int> a)
				{
					this.a = a;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var b = container.Get<B>();
				Assert.That(b.a, Is.InstanceOf<A<int>>());
				Assert.That(b.a, Is.SameAs(container.Get<IA<int>>()));
			}
		}

		public class CanBindDefinitions : NotConfiguredGenericsTest
		{
			public interface IA<T>
			{
			}

			public class A1<T> : IA<T>
			{
			}

			public class A2<T> : IA<T>
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.Bind(typeof (IA<>), typeof (A2<>)));
				Assert.That(container.Get<IA<int>>(), Is.InstanceOf<A2<int>>());
			}
		}

		public class FitlerImplementationsUsingGenericArgumentsMatching : NotConfiguredGenericsTest
		{
			public interface IA<T>
			{
			}

			public class A1 : IA<int>
			{
			}

			public class A2 : IA<H>
			{
			}

			public class H
			{
			}

			public class B
			{
				public readonly IA<int> a;

				public B(IA<int> a)
				{
					this.a = a;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<B>().a, Is.InstanceOf<A1>());
			}
		}

		public class CheckConstraints : NotConfiguredGenericsTest
		{
			public interface IA<T1>
			{
			}

			public interface IRestriction
			{
			}

			public class A1<T2> : IA<T2>
				where T2 : IRestriction
			{
			}

			public class A2<T3> : IA<T3>
				where T3: new()
			{
			}

			public class A3<T4> : IA<T4>
			{
			}

			public class S
			{
				public readonly int value;

				public S(int value)
				{
					this.value = value;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var actual = container.GetAll<IA<S>>().ToArray();
				Assert.That(actual.Length, Is.EqualTo(1));
				Assert.That(actual[0], Is.InstanceOf<A3<S>>());
			}
		}
	}
}