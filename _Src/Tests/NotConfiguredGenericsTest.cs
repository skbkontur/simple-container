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
	}
}