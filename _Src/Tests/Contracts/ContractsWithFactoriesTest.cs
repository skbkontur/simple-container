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

		public class FactoriesUseOnlyRequiredContracts : ContractsWithFactoriesTest
		{
			[TestContract("c1")]
			public class A
			{
				public readonly B b;

				public A(Func<B> createB)
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
				var c1 = Container(b => b.BindDependencies<B>(new { parameter = 42 }));
				var a1 = c1.Resolve<A>();
				Assert.That(a1.Single().b.parameter, Is.EqualTo(42));
				Assert.That(a1.GetConstructionLog(), Is.EqualTo("A\r\n\tFunc<B>\r\n\t() => B\r\n\t\tparameter -> 42"));

				var c2 = Container(b => b.Contract("c1").BindDependencies<B>(new { parameter = 43 }));
				var a2 = c2.Resolve<A>();
				Assert.That(a2.Single().b.parameter, Is.EqualTo(43));
				Assert.That(a2.GetConstructionLog(), Is.EqualTo("A[c1]\r\n\tFunc<B>[c1]\r\n\t() => B[c1]\r\n\t\tparameter -> 43"));
			}
		}

		public class FactoryForServiceWithArgument : ContractsWithFactoriesTest
		{
			public class A
			{
				public readonly int p1;
				public readonly B b;

				public A(int p1, B b)
				{
					this.p1 = p1;
					this.b = b;
				}
			}

			public class B
			{
				public readonly int p2;

				public B(int p2)
				{
					this.p2 = p2;
				}
			}

			public class C
			{
				public readonly Func<A> createA;

				public C(Func<A> createA)
				{
					this.createA = createA;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.Contract("c1").BindDependencies<B>(new {p2 = 2}));
				var c = container.Resolve<C>("c1");
				Assert.That(c.GetConstructionLog(), Is.EqualTo("C[c1]\r\n\tFunc<A>[c1]"));
			}
		}

		public class TrackDependenciesForLazyServices : ContractsWithFactoriesTest
		{
			public class A
			{
				public readonly Lazy<B> lazyB;

				public A(Lazy<B> lazyB)
				{
					this.lazyB = lazyB;
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
				var container = Container(b => b.Contract("c1").BindDependencies<B>(new {parameter = 42}));
				var a = container.Resolve<A>("c1");
				Assert.That(a.GetConstructionLog(), Is.EqualTo("A[c1]\r\n\tLazy<B>[c1]"));
			}
		}

		public class MergeConstructionLogForLazies : ContractsWithFactoriesTest
		{
			public class A
			{
				public readonly B b;

				public A(Lazy<B> lazyB)
				{
					b = lazyB.Value;
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
				var container = Container();
				var a = container.Resolve<A>();
				Assert.That(a.GetConstructionLog(), Is.EqualTo("!A\r\n\tLazy<B>\r\n\t!() => B\r\n\t\t!parameter <---------------"));
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