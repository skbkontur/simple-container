using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.ConstructionLog
{
	public abstract class ConstructionLogGroupingByStackTest : SimpleContainerTestBase
	{
		public class ConstructionLogForManyImplicitDependencies : ConstructionLogGroupingByStackTest
		{
			public class A
			{
				public IEnumerable<IB> b;

				public A(IContainer container)
				{
					b = container.GetAll<IB>();
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
				var a = container.Resolve<A>();
				Assert.That(a.GetConstructionLog(), Is.EqualTo(TestHelpers.FormatMessage(@"
A
	IContainer
	() => IB++
		B1
		B2")));
			}
		}

		public class MergeConstructionLogFromDifferentContainerInstances : ConstructionLogGroupingByStackTest
		{
			public class A
			{
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
				var c1 = Container();
				using (var c2 = c1.Clone(b => b.Bind(c => c1.Get<A>())))
				{
					var b = c2.Resolve<B>();
					Assert.That(b.Single().a, Is.SameAs(c1.Get<A>()));
					Assert.That(b.GetConstructionLog(), Is.EqualTo(TestHelpers.FormatMessage(@"
B
	A
		() => A - container boundary")));
				}
			}
		}

		public class MergeConstructionLogFromInjectedContainer : ConstructionLogGroupingByStackTest
		{
			public class A
			{
				public readonly B b;

				public A(IContainer container)
				{
					b = container.Get<B>();
				}
			}

			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var a = container.Resolve<A>();
				Assert.That(a.Single().b, Is.SameAs(container.Get<B>()));
				Assert.That(a.GetConstructionLog(), Is.EqualTo(TestHelpers.FormatMessage(@"
A
	IContainer
	() => B")));
			}
		}
	}
}