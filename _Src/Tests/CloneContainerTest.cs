using System;
using NUnit.Framework;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class CloneContainerTest : SimpleContainerTestBase
	{
		public class Simple : CloneContainerTest
		{
			private static int counter;

			public class Hoster
			{
				public readonly IContainer container;

				public Hoster(IContainer container)
				{
					this.container = container;
				}
			}

			public class ComponentWrap
			{
				public readonly Component component;
				private readonly int myCounter = counter++;

				public ComponentWrap(Component component)
				{
					this.component = component;
					LogBuilder.Append("ComponentWrap" + myCounter + ".ctor ");
				}
			}

			public class Component : IDisposable
			{
				private readonly int myCounter = counter++;

				public Component()
				{
					LogBuilder.Append("Component" + myCounter + ".ctor ");
				}

				public void Dispose()
				{
					LogBuilder.Append("Component" + myCounter + ".Dispose ");
				}
			}

			[Test]
			public void Test()
			{
				using (var container = Container())
				{
					Assert.That(LogBuilder.ToString(), Is.EqualTo(""));
					var hoster = container.Get<Hoster>();
					Assert.That(LogBuilder.ToString(), Is.EqualTo(""));
					var outerWrap = container.Get<ComponentWrap>();
					Assert.That(LogBuilder.ToString(), Is.EqualTo("Component0.ctor ComponentWrap1.ctor "));
					LogBuilder.Clear();
					ComponentWrap clone1;
					using (var clonedContainer = hoster.container.Clone(null))
					{
						clone1 = clonedContainer.Get<ComponentWrap>();
						Assert.That(clone1, Is.Not.SameAs(outerWrap));
						Assert.That(LogBuilder.ToString(), Is.EqualTo("Component2.ctor ComponentWrap3.ctor "));
						Assert.That(container.Get<ComponentWrap>(), Is.SameAs(outerWrap));
						LogBuilder.Clear();
					}
					Assert.That(LogBuilder.ToString(), Is.EqualTo("Component2.Dispose "));
					LogBuilder.Clear();
					using (var clonedContainer = hoster.container.Clone(null))
					{
						Assert.That(clonedContainer.Get<ComponentWrap>(), Is.Not.SameAs(outerWrap));
						Assert.That(clonedContainer.Get<ComponentWrap>(), Is.Not.SameAs(clone1));
						Assert.That(clonedContainer.Get<ComponentWrap>(), Is.SameAs(clonedContainer.Get<ComponentWrap>()));
						Assert.That(LogBuilder.ToString(), Is.EqualTo("Component4.ctor ComponentWrap5.ctor "));
						LogBuilder.Clear();
					}
					Assert.That(LogBuilder.ToString(), Is.EqualTo("Component4.Dispose "));
					LogBuilder.Clear();
				}
				Assert.That(LogBuilder.ToString(), Is.EqualTo("Component0.Dispose "));
			}
		}

		public class OverrideContractConfiguration : CloneContainerTest
		{
			public class A
			{
				public readonly B b;

				public A([TestContract("x")] B b)
				{
					this.b = b;
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

			public class C
			{
				public readonly int parameter;

				public C(int parameter)
				{
					this.parameter = parameter;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.Contract("x").BindDependency<B>("parameter", 12));
				using (var clonedContainer = container.Clone(b => b.BindDependency<C>("parameter", 13)))
				{
					Assert.That(clonedContainer.Get<A>().b.parameter, Is.EqualTo(12));
					Assert.That(clonedContainer.Get<C>().parameter, Is.EqualTo(13));
				}
			}
		}

		public class CanOverrideConfiguration : CloneContainerTest
		{
			public class A
			{
				public readonly int parameter;

				public A(int parameter)
				{
					this.parameter = parameter;
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
				var container = Container(b =>
				{
					b.BindDependency<A>("parameter", 1);
					b.BindDependency<B>("parameter", 2);
				});
				Assert.That(container.Get<A>().parameter, Is.EqualTo(1));
				Assert.That(container.Get<B>().parameter, Is.EqualTo(2));
				using (var clonedContainer = container.Clone(b => b.BindDependency<A>("parameter", 3)))
				{
					Assert.That(clonedContainer.Get<A>().parameter, Is.EqualTo(3));
					Assert.That(clonedContainer.Get<B>().parameter, Is.EqualTo(2));
				}
			}
		}
	}
}