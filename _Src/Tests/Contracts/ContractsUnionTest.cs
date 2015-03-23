using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Infection;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.Contracts
{
	public abstract class ContractsUnionTest : SimpleContainerTestBase
	{
		public class MultipleUnionOfDefinitionsOfSingleDeclarationIsProhibited : ContractsUnionTest
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
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder b)
				{
					b.Contract("c1").Contract("c2").UnionOf("x1", "x2");
					b.Contract("c2").UnionOf("x3", "x4");
				});
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<A>());
				Assert.That(error.Message,
					Is.EqualTo(
						"contract [c2] has conflicting unions [x1,x2] and [x3,x4]\r\n\r\n!A->[c1]\r\n\t!B->[c1->c2] <---------------"));
			}
		}

		public class NestedUnions : ContractsUnionTest
		{
			public class A
			{
				public readonly BWrap[] bWraps;

				public A([TestContract("u1")] BWrap[] bWraps)
				{
					this.bWraps = bWraps;
				}
			}

			public class BWrap
			{
				public readonly IB b;

				public BWrap(IB b)
				{
					this.b = b;
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

			public class B3 : IB
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder b)
				{
					b.Contract("u1").UnionOf("c1", "c2");
					b.Contract("c1").UnionOf("c3", "c4");
					b.Contract("c2").UnionOf("c4", "c5");

					b.Contract("c3").Bind<IB, B1>();
					b.Contract("c4").Bind<IB, B2>();
					b.Contract("c5").Bind<IB, B3>();
				});

				var instance = container.Resolve<A>();
				Assert.That(instance.Single().bWraps.Length, Is.EqualTo(3));
			}
		}

		public class CartesianProductAdjacentUnionedContracts : ContractsUnionTest
		{
			public class Host
			{
				public readonly A[] instances;

				public Host([TestContract("union1")] A[] instances)
				{
					this.instances = instances;
				}
			}

			[TestContract("union2")]
			public class A
			{
				public readonly int parameter;
				public readonly B b;

				public A(int parameter, B b)
				{
					this.parameter = parameter;
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

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder b)
				{
					b.Contract("union1").UnionOf("c1", "c2");
					b.Contract("union2").UnionOf("c3", "c4");

					b.Contract("c1").BindDependency<A>("parameter", 1);
					b.Contract("c2").BindDependency<A>("parameter", 2);
					b.Contract("c3").BindDependency<B>("parameter", 3);
					b.Contract("c4").BindDependency<B>("parameter", 4);
				});

				var host = container.Get<Host>();
				Assert.That(host.instances.Length, Is.EqualTo(4));

				Assert.That(host.instances[0].parameter, Is.EqualTo(1));
				Assert.That(host.instances[0].b.parameter, Is.EqualTo(3));

				Assert.That(host.instances[1].parameter, Is.EqualTo(1));
				Assert.That(host.instances[1].b.parameter, Is.EqualTo(4));

				Assert.That(host.instances[2].parameter, Is.EqualTo(2));
				Assert.That(host.instances[2].b.parameter, Is.EqualTo(3));

				Assert.That(host.instances[3].parameter, Is.EqualTo(2));
				Assert.That(host.instances[3].b.parameter, Is.EqualTo(4));
			}
		}

		public class CanExplicitlyQueryForUnionedContract : ContractsUnionTest
		{
			public class Host
			{
				public readonly IA a;

				public Host(IA a)
				{
					this.a = a;
				}
			}

			public interface IA
			{
			}

			public class A1 : IA
			{
			}

			public class A2 : IA
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("unioned").UnionOf("c1", "c2");
					builder.Contract("c1").Bind<IA, A1>();
					builder.Contract("c2").Bind<IA, A2>();
				});
				var hosts = container.GetAll<Host>("unioned").ToArray();
				Assert.That(hosts.Length, Is.EqualTo(2));
				Assert.That(hosts[0].a, Is.InstanceOf<A1>());
				Assert.That(hosts[1].a, Is.InstanceOf<A2>());
				Assert.That(hosts[0].a, Is.SameAs(container.Get<IA>("c1")));
				Assert.That(hosts[1].a, Is.SameAs(container.Get<IA>("c2")));
			}
		}

		public class ContractsCanBeUnioned : ContractsUnionTest
		{
			public class AllWrapsHost
			{
				public readonly ServiceWrap[] wraps;

				public AllWrapsHost([TestContract("composite-contract")] IEnumerable<ServiceWrap> wraps)
				{
					this.wraps = wraps.ToArray();
				}
			}

			public class ServiceWrap
			{
				public readonly IService service;

				public ServiceWrap(IService service)
				{
					this.service = service;
				}
			}

			public interface IService
			{
			}

			public class Service1 : IService
			{
			}

			public class Service2 : IService
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("composite-contract").UnionOf("service1Contract", "service2Contract");

					builder.Contract("service1Contract").Bind<IService, Service1>();
					builder.Contract("service2Contract").Bind<IService, Service2>();
				});
				var wrap = container.Get<AllWrapsHost>();
				Assert.That(wrap.wraps.Length, Is.EqualTo(2));
				Assert.That(wrap.wraps[0].service, Is.InstanceOf<Service1>());
				Assert.That(wrap.wraps[1].service, Is.InstanceOf<Service2>());
			}
		}

		public class ClearOldUnionContracts : ContractsUnionTest
		{
			public class AllWrapsHost
			{
				public readonly ServiceWrap[] wraps;

				public AllWrapsHost([TestContract("composite-contract")] IEnumerable<ServiceWrap> wraps)
				{
					this.wraps = wraps.ToArray();
				}
			}

			public class ServiceWrap
			{
				public readonly IService service;

				public ServiceWrap(IService service)
				{
					this.service = service;
				}
			}

			public interface IService
			{
			}

			public class Service1 : IService
			{
			}

			public class Service2 : IService
			{
			}

			public class CompositeContractConfigurator : IContainerConfigurator
			{
				public void Configure(ConfigurationContext context, ContainerConfigurationBuilder builder)
				{
					builder.Contract("c1").Bind<IService, Service1>();
					builder.Contract("c2").Bind<IService, Service2>();
					builder.Contract("composite-contract").UnionOf("c1", "c2");
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.Contract("composite-contract").UnionOf("c2"));
				Assert.That(container.Get<AllWrapsHost>().wraps.Length, Is.EqualTo(2));

				container = Container(b => b.Contract("composite-contract").UnionOf(true, "c2"));
				Assert.That(container.Get<AllWrapsHost>().wraps.Length, Is.EqualTo(1));
			}
		}

		public class ContractUnionGeneric : ContractsUnionTest
		{
			public class Contract1 : RequireContractAttribute
			{
				public Contract1()
					: base("c1")
				{
				}
			}

			public class Contract2 : RequireContractAttribute
			{
				public Contract2()
					: base("c2")
				{
				}
			}

			public class AllWrapsHost
			{
				public readonly ServiceWrap[] wraps;

				public AllWrapsHost([TestContract("composite-contract")] IEnumerable<ServiceWrap> wraps)
				{
					this.wraps = wraps.ToArray();
				}
			}

			public class ServiceWrap
			{
				public readonly IService service;

				public ServiceWrap(IService service)
				{
					this.service = service;
				}
			}

			public interface IService
			{
			}

			public class Service1 : IService
			{
			}

			public class Service2 : IService
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("composite-contract").Union<Contract1>().Union<Contract2>();
					builder.Contract<Contract1>().Bind<IService, Service1>();
					builder.Contract<Contract2>().Bind<IService, Service2>();
				});
				var wrap = container.Get<AllWrapsHost>();
				Assert.That(wrap.wraps.Length, Is.EqualTo(2));
				Assert.That(wrap.wraps[0].service, Is.InstanceOf<Service1>());
				Assert.That(wrap.wraps[1].service, Is.InstanceOf<Service2>());
			}
		}

		public class UnionContractsWithNonContractDependentServices : ContractsUnionTest
		{
			public class ServiceWrap
			{
				public readonly Service[] wraps;

				public ServiceWrap([TestContract("composite-contract")] IEnumerable<Service> wraps)
				{
					this.wraps = wraps.ToArray();
				}
			}

			public class Service
			{
			}

			public class OtherService
			{
				public readonly int parameter;

				public OtherService(int parameter)
				{
					this.parameter = parameter;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("composite-contract").UnionOf("service1Contract", "service2Contract");

					builder.Contract("service1Contract").BindDependency<OtherService>("parameter", 1);
					builder.Contract("service2Contract").BindDependency<OtherService>("parameter", 2);
				});
				var wrap = container.Get<ServiceWrap>();
				Assert.That(wrap.wraps.Length, Is.EqualTo(1));
				Assert.That(wrap.wraps[0], Is.SameAs(container.Get<Service>()));
			}
		}

		public class RequiredUnionedContracts : ContractsUnionTest
		{
			public class A
			{
				public readonly B b;
				public readonly C y1C;
				public readonly C y2C;

				public A([TestContract("x")] B b, [TestContract("y1")] C y1C, [TestContract("y2")] C y2C)
				{
					this.b = b;
					this.y1C = y1C;
					this.y2C = y2C;
				}
			}

			public class B
			{
				public readonly IEnumerable<C> enumerable;

				public B([TestContract("unioned")] IEnumerable<C> enumerable)
				{
					this.enumerable = enumerable;
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
				var container = Container(delegate(ContainerConfigurationBuilder b)
				{
					b.Contract("x");
					b.Contract("unioned").UnionOf("y1", "y2");
					b.Contract("x", "y1").BindDependency<C>("parameter", 1);
					b.Contract("x", "y2").BindDependency<C>("parameter", 2);
					b.Contract("y1").BindDependency<C>("parameter", 3);
					b.Contract("y2").BindDependency<C>("parameter", 4);
				});
				var a = container.Get<A>();
				Assert.That(a.b.enumerable.Select(x => x.parameter).ToArray(), Is.EquivalentTo(new[] { 1, 2 }));
				Assert.That(a.y1C.parameter, Is.EqualTo(3));
				Assert.That(a.y2C.parameter, Is.EqualTo(4));
			}
		}

		public class ManyRequiredAndUnionedContracts : ContractsUnionTest
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
				public readonly IEnumerable<C> enumerable;

				public B([TestContract("unioned")] IEnumerable<C> enumerable)
				{
					this.enumerable = enumerable;
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
					b.Contract("x");
					b.Contract("unioned").UnionOf("y");
					b.Contract("y").BindDependency<C>("context", "x");
					b.Contract("x", "y").BindDependency<C>("context", "xy");
				});
				var a = container.Get<A>();
				Assert.That(a.b.enumerable.Single().context, Is.EqualTo("xy"));
			}
		}
	}
}