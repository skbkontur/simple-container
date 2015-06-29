using System.Linq;
using NUnit.Framework;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.Generics
{
	public class GenericClassImplementingGenericIntefaceWithDifferentArguments : SimpleContainerTestBase
	{
		public interface IConsumer
		{
		}

		public class Consumer<T> : IConsumer
		{
			public readonly IA<T> a;

			public Consumer(IA<T> a)
			{
				this.a = a;
			}
		}

		public interface IA<T>
		{
		}

		public class A<T> : IA<X<T>>, IA<Y<T>>, IA<T>
		{
			public readonly B<T> b;

			public A(B<T> b)
			{
				this.b = b;
			}
		}

		public abstract class B<T>
		{
		}

		public class B1 : B<int>
		{
		}

		public class X<T>
		{
		}

		public class Y<T>
		{
		}

		[Test]
		public void Test()
		{
			var container = Container();
			var implTypes = container.GetAll<IConsumer>().Select(x => x.GetType().GetGenericArguments()[0]);
			Assert.That(implTypes, Is.EquivalentTo(new[] {typeof (X<int>), typeof (Y<int>), typeof (int)}));
		}
	}
}