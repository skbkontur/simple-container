using System;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.Contracts
{
	public abstract class ContractsWithFactoriesTest : SimpleContainerTestBase
	{
		public class ContractsWorksWithFactories : ContractsWithFactoriesTest
		{
			public class A
			{
				public readonly B bc1;
				public readonly B bc2;

				public A([TestContract("c1")] B bc1, [TestContract("c2")] B bc2)
				{
					this.bc1 = bc1;
					this.bc2 = bc2;
				}
			}

			public class B
			{
				public readonly Func<IInterface> getInterface;

				public B(Func<IInterface> getInterface)
				{
					this.getInterface = getInterface;
				}
			}

			public interface IInterface
			{
			}

			public class C : IInterface
			{
			}

			public class D : IInterface
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("c1").Bind<IInterface, C>();
					builder.Contract("c2").Bind<IInterface, D>();
				});
				var a = container.Get<A>();
				Assert.That(a.bc1.getInterface(), Is.InstanceOf<C>());
				Assert.That(a.bc2.getInterface(), Is.InstanceOf<D>());
			}
		}

		public class ContractsWorksWithFactoriesWithArguments : ContractsWithFactoriesTest
		{
			public class A
			{
				public readonly B bc1;
				public readonly B bc2;

				public A([TestContract("c1")] B bc1, [TestContract("c2")] B bc2)
				{
					this.bc1 = bc1;
					this.bc2 = bc2;
				}
			}

			public class B
			{
				public readonly Func<object, FactoryResult> getResult;

				public B(Func<object, FactoryResult> getResult)
				{
					this.getResult = getResult;
				}
			}

			public class FactoryResult
			{
				public IInterface intf;
				public int value;

				public FactoryResult(IInterface intf, int value)
				{
					this.intf = intf;
					this.value = value;
				}
			}

			public interface IInterface
			{
			}

			public class C : IInterface
			{
			}

			public class D : IInterface
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("c1").Bind<IInterface, C>();
					builder.Contract("c2").Bind<IInterface, D>();
				});
				var a = container.Get<A>();
				var result1 = a.bc1.getResult(new {value = 1});
				var result2 = a.bc2.getResult(new {value = 2});
				Assert.That(result1.value, Is.EqualTo(1));
				Assert.That(result2.value, Is.EqualTo(2));
				Assert.That(result1.intf, Is.InstanceOf<C>());
				Assert.That(result2.intf, Is.InstanceOf<D>());
			}
		}

		public class NewInstanceOfServiceWithUnusedContract : ContractsWithFactoriesTest
		{
			[TestContract("a")]
			public class A
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<A>(), Is.Not.SameAs(container.Create<A>()));
			}
		}

		public class NewInstanceOfServiceWithUnusedContractViaInterface : ContractsWithFactoriesTest
		{
			public class A : IA
			{
			}

			[TestContract("a")]
			public interface IA
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<A>(), Is.Not.SameAs(container.Create<IA>()));
			}
		}

		public class CaptureFactoryContract : ContractsWithFactoriesTest
		{
			public class A
			{
				public B b;

				public A([TestContract("a")] Func<B> createB)
				{
					b = createB();
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
				var container = Container(delegate(ContainerConfigurationBuilder b)
				{
					b.BindDependency<B>("parameter", 1);
					b.Contract("a").BindDependency<B>("parameter", 2);
				});
				Assert.That(container.Get<A>().b.parameter, Is.EqualTo(2));
			}
		}
	}
}