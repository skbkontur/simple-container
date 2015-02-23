using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Infection;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class BuildUpTest : SimpleContainerTestBase
	{
		public class CanInjectEnumerableByAttribute : BuildUpTest
		{
			[Inject]
			public IEnumerable<IInterface> Interfaces { get; private set; }

			[Test]
			public void Test()
			{
				var container = Container();
				container.BuildUp(this, null);
				Assert.That(Interfaces, Is.EquivalentTo(new IInterface[] {container.Get<Impl1>(), container.Get<Impl2>()}));
			}

			public interface IInterface
			{
			}

			public class Impl1 : IInterface
			{
			}

			public class Impl2 : IInterface
			{
			}
		}

		public class CanInjectFactory : BuildUpTest
		{
			[Inject] private Func<B> createB;

			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				container.BuildUp(this, null);
				Assert.That(createB(), Is.Not.Null);
			}
		}

		public class CanInjectFactoryWithContract : BuildUpTest
		{
			[Inject] [TestContract("x")] private Func<B> createB;

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
				var container = Container(b => b.Contract("x").BindDependency<B>("parameter", 42));
				container.BuildUp(this, null);
				Assert.That(createB().parameter, Is.EqualTo(42));
			}
		}

		public class CanRunBuilduppedObject : BuildUpTest
		{
			public class A : IComponent
			{
				public static bool runCalled;

				public void Run()
				{
					runCalled = true;
				}
			}

			[Inject] private A a;

			[Test]
			public void Test()
			{
				var container = Container();
				container.BuildUp(this, null).Run();
				Assert.That(a, Is.Not.Null);
				Assert.That(A.runCalled);
			}
		}

		public class GracefulBuildUpExceptions : BuildUpTest
		{
			public class A
			{
			}

			[Inject] private A a;

			[Test]
			public void Test()
			{
				var container = Container(b => b.DontUse<A>());
				var error = Assert.Throws<SimpleContainerException>(() => container.BuildUp(this, new String[0]));
				Assert.That(a, Is.Null);
				Assert.That(error.Message, Is.EqualTo("can't resolve member [GracefulBuildUpExceptions.a]"));
				Assert.That(error.InnerException.Message, Is.EqualTo("no implementations for A\r\nA! - DontUse"));
			}
		}

		public class BuildUpWithContracts : BuildUpTest
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
				[Inject] public A a;
				[Inject] [TestContract("c2")] public A ac2;
			}

			public class C
			{
				[Inject] public A a;
				[Inject] [TestContract("c1")] public A ac1;
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("c1").BindDependency<A>("parameter", 1);
					builder.Contract("c2").BindDependency<A>("parameter", 2);
				});

				var b = new B();
				container.BuildUp(b, new[] {"c1"});
				Assert.That(b.a.parameter, Is.EqualTo(1));
				Assert.That(b.ac2.parameter, Is.EqualTo(2));

				var c = new C();
				container.BuildUp(c, new[] {"c2"});
				Assert.That(c.a.parameter, Is.EqualTo(2));
				Assert.That(c.ac1.parameter, Is.EqualTo(1));

				Assert.That(b.ac2, Is.Not.SameAs(c.ac1));
				Assert.That(b.a, Is.Not.SameAs(c.a));

				Assert.That(b.a, Is.SameAs(c.ac1));
				Assert.That(c.a, Is.SameAs(b.ac2));
			}
		}
	}
}