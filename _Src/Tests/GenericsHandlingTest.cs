using NUnit.Framework;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class GenericsHandlingTest : SimpleContainerTestBase
	{
		public class CanCloseImplementationByInterface : GenericsHandlingTest
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
				Assert.That(container.Get<B>().a, Is.SameAs(container.Get<IA<int>>()));
			}
		}

		public class CanCloseImplementationByInterfaceBUG : GenericsHandlingTest
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