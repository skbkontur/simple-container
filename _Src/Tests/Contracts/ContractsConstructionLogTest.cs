using System;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.Contracts
{
	public abstract class ContractsConstructionLogTest : SimpleContainerTestBase
	{
		public class ContractFormatting : ContractsConstructionLogTest
		{
			public class Wrap
			{
				public readonly Service service;

				public Wrap([TestContract("c1")] Service service)
				{
					this.service = service;
				}
			}

			public class Service
			{
				public readonly SingletonService singletonService;
				public readonly IInterface @interface;

				public Service(SingletonService singletonService, IInterface @interface)
				{
					this.singletonService = singletonService;
					this.@interface = @interface;
				}
			}

			public class SingletonService
			{
			}

			public interface IInterface
			{
			}

			public interface IUnimplemented
			{
			}

			public class Impl1 : IInterface
			{
				public readonly IUnimplemented unimplemented;

				public Impl1(IUnimplemented unimplemented)
				{
					this.unimplemented = unimplemented;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(c => c.Contract("c1").Bind<IInterface, Impl1>());
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<Wrap>());
				const string expectedMessage =
					"no instances for [Wrap] because [IUnimplemented] has no instances\r\n\r\n!Wrap\r\n\t!Service[c1]\r\n\t\tSingletonService\r\n\t\t!IInterface[c1]\r\n\t\t\t!Impl1\r\n\t\t\t\t!IUnimplemented - has no implementations";
				Assert.That(error.Message, Is.EqualTo(expectedMessage));
			}
		}

		public class ConstructionLogForSpecificContract : ContractsConstructionLogTest
		{
			public class Wrap
			{
				public readonly A a1;
				public readonly A a2;

				public Wrap([TestContract("a1")] A a1, [TestContract("a2")] A a2)
				{
					this.a1 = a1;
					this.a2 = a2;
				}
			}

			public class A
			{
				public readonly int parameter;

				public A(int parameter)
				{
					this.parameter = parameter;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("a1");
					builder.BindDependency<A>("parameter", 53);
					builder.Contract("a2").BindDependency<A>("parameter", 52);
				});
				container.Get<Wrap>();
				Assert.That(container.Resolve<A>("a2").GetConstructionLog(), Is.EqualTo("A[a2]\r\n\tparameter -> 52"));
			}
		}

		public class ConstructionLogForFactory : ContractsConstructionLogTest
		{
			[TestContract("a")]
			public class A
			{
				public readonly Func<B> func;

				public A(Func<B> func)
				{
					this.func = func;
				}
			}

			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.Contract("a"));
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo("A[a]\r\n\tFunc<B>[a]"));
			}
		}

		public class DoNotDuplicateRequiredContractsInConstructionLog : ContractsConstructionLogTest
		{
			[TestContract("c1")]
			public class A
			{
				public readonly B b;

				public A([TestContract("c2")] B b)
				{
					this.b = b;
				}
			}

			public class B
			{
				public readonly int parameter;
				public readonly C c;

				public B(int parameter, C c)
				{
					this.parameter = parameter;
					this.c = c;
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
				var container = Container(b =>
				{
					b.Contract("c1").Contract("c2").BindDependency<B>("parameter", 14);
					b.Contract("c2").BindDependency<C>("parameter", 55);
				});
				Assert.That(container.Get<A>().b.parameter, Is.EqualTo(14));
				Assert.That(container.Get<A>().b.c.parameter, Is.EqualTo(55));
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.StringStarting("A[c1]\r\n\tB[c1->c2]"));
			}
		}
	}
}