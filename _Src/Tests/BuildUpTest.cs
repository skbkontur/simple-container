using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Hosting;
using SimpleContainer.Infection;

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
			[Inject] [RequireContract("x")] private Func<B> createB;

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
				container.BuildUp(this, null);
				container.RunComponents(GetType());
				Assert.That(A.runCalled);
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

			[Inject] private A a;
			[Inject] [RequireContract("c1")] private A ac1;
			[Inject] [RequireContract("c2")] private A ac2;

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("c1").BindDependency<A>("parameter", 1);
					builder.Contract("c2").BindDependency<A>("parameter", 2);
				});
				container.BuildUp(this, new[] {"c1"});
				var localA = a;
				var localAc1 = ac1;
				var localAc2 = ac2;

				Assert.That(localA.parameter, Is.EqualTo(1));
				Assert.That(localAc1.parameter, Is.EqualTo(1));
				Assert.That(localAc2.parameter, Is.EqualTo(2));

				Assert.That(localAc1, Is.SameAs(a));

				container.BuildUp(this, new[] { "c2" });

				Assert.That(a.parameter, Is.EqualTo(2));
				Assert.That(ac1.parameter, Is.EqualTo(1));
				Assert.That(ac2.parameter, Is.EqualTo(2));

				Assert.That(a, Is.SameAs(localAc2));
				Assert.That(ac1, Is.EqualTo(localAc1));
				Assert.That(ac2, Is.EqualTo(localAc2));
			}
		}
	}
}