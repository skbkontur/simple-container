using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Infection;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.Contracts
{
	public abstract class ContractsNestingTest : SimpleContainerTestBase
	{
		public class NestedRequiredContracts : ContractsNestingTest
		{
			public class A
			{
				public readonly B b;
				public readonly C cx;
				public readonly C cy;
				public readonly C c;

				public A([TestContract("x")] B b, [TestContract("x")] C cx, [TestContract("y")] C cy, C c)
				{
					this.b = b;
					this.cx = cx;
					this.cy = cy;
					this.c = c;
				}
			}

			public class B
			{
				public readonly C c;

				public B([TestContract("y")] C c)
				{
					this.c = c;
				}
			}

			public class C
			{
				public readonly string context;

				public C(string context)
				{
					this.context = context;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder b)
				{
					b.Contract("x", "y").BindDependency<C>("context", "xy");
					b.Contract("x").BindDependency<C>("context", "x");
					b.Contract("y").BindDependency<C>("context", "y");
					b.BindDependency<C>("context", "empty");
				});
				var a = container.Get<A>();
				Assert.That(a.b.c.context, Is.EqualTo("xy"));
				Assert.That(a.cx.context, Is.EqualTo("x"));
				Assert.That(a.cy.context, Is.EqualTo("y"));
				Assert.That(a.c.context, Is.EqualTo("empty"));
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.StringStarting("A\r\n\tB[x]\r\n\t\tC[x->y]\r\n\t\t\tcontext -> xy"));
			}
		}

		public class CanUseContractOnContractConfiguratorForRestriction : ContractsNestingTest
		{
			public class Contract1Attribute : RequireContractAttribute
			{
				public Contract1Attribute()
					: base("contract-1")
				{
				}
			}

			public class Contract2Attribute : RequireContractAttribute
			{
				public Contract2Attribute()
					: base("contract-2")
				{
				}
			}

			public class A
			{
				public readonly B contract1B;
				public readonly B b;

				public A([Contract1] B contract1B, B b)
				{
					this.contract1B = contract1B;
					this.b = b;
				}
			}

			public class B
			{
				public readonly C c;

				public B([Contract2] C c)
				{
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

			public class CConfigurator : IServiceConfigurator<C>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<C> builder)
				{
					builder.Contract<Contract1Attribute>().Contract<Contract2Attribute>().Dependencies(new { parameter = 1 });
					builder.Contract<Contract2Attribute>().Dependencies(new { parameter = 2 });
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var a = container.Get<A>();
				Assert.That(a.contract1B.c.parameter, Is.EqualTo(1));
				Assert.That(a.b.c.parameter, Is.EqualTo(2));
			}
		}

		public class NotDefinedContractsCanBeUsedLaterToRestrictOtherContracts : ContractsNestingTest
		{
			[TestContract("a")]
			public class A
			{
				public readonly X x;

				public A(X x)
				{
					this.x = x;
				}
			}

			[TestContract("b")]
			public class B
			{
				public readonly X x;

				public B(X x)
				{
					this.x = x;
				}
			}

			[TestContract("x")]
			public class X
			{
				public readonly int parameter;

				public X(int parameter)
				{
					this.parameter = parameter;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("a", "x").BindDependency<X>("parameter", 1);
					builder.Contract("b", "x").BindDependency<X>("parameter", 2);
				});
				Assert.That(container.Get<A>().x.parameter, Is.EqualTo(1));
				Assert.That(container.Get<B>().x.parameter, Is.EqualTo(2));
			}
		}

		public class CanDeclareContractsChain : ContractsNestingTest
		{
			[ContractsSequence(typeof (ContractX), typeof (ContractY))]
			public class Axy
			{
				public readonly B b;

				public Axy(B b)
				{
					this.b = b;
				}
			}


			public class Ayx
			{
				public readonly B b;

				public Ayx([ContractsSequence(typeof (ContractY), typeof (ContractX))] B b)
				{
					this.b = b;
				}
			}

			public class B
			{
				public readonly string contracts;

				public B(string contracts)
				{
					this.contracts = contracts;
				}
			}

			public class ContractX : RequireContractAttribute
			{
				public ContractX() : base("x")
				{
				}
			}

			public class ContractY : RequireContractAttribute
			{
				public ContractY() : base("y")
				{
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract<ContractX>().Contract<ContractY>().BindDependencies<B>(new {contracts = "xy"});
					builder.Contract<ContractY>().Contract<ContractX>().BindDependencies<B>(new {contracts = "yx"});
				});
				Assert.That(container.Get<Axy>().b.contracts, Is.EqualTo("xy"));
				Assert.That(container.Get<Ayx>().b.contracts, Is.EqualTo("yx"));
			}
		}

		public class ConditionalContractsRedefinitionIsProhibited : ContractsNestingTest
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

			[TestContract("c2")]
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
				var container = Container(b => b.Contract("c1").Contract("c2").BindDependency<B>("parameter", 42));
				var exception = Assert.Throws<SimpleContainerException>(() => container.Get<A>());
				Assert.That(exception.Message,
					Is.EqualTo("contract [c2] already declared, stack\r\n\tA[c1]\r\n\tB[c2->c2]\r\n\r\n!A\r\n\t!B <---------------"));
			}
		}
	}
}