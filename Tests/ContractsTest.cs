using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Implementation;
using SimpleContainer.Infection;

namespace SimpleContainer.Tests
{
	public abstract class ContractsTest : SimpleContainerTestBase
	{
		public class RequireContractTest : ContractsTest
		{
			public class ContainerClass1
			{
				public readonly OuterClass outerClass;

				public ContainerClass1([RequireContract("c1")] OuterClass outerClass)
				{
					this.outerClass = outerClass;
				}
			}

			public class ContainerClass2
			{
				public readonly OuterClass outerClass;

				public ContainerClass2([RequireContract("c2")] OuterClass outerClass)
				{
					this.outerClass = outerClass;
				}
			}

			public class OuterClass
			{
				public readonly InnerClass innerClass;

				public OuterClass(InnerClass innerClass)
				{
					this.innerClass = innerClass;
				}
			}

			public class InnerClass
			{
				public readonly IInterface @interface;

				public InnerClass(IInterface @interface)
				{
					this.@interface = @interface;
				}
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

			[Test]
			public void Test()
			{
				var container = Container(c =>
				{
					c.Contract("c1").Bind<IInterface, Impl1>();
					c.Contract("c2").Bind<IInterface, Impl2>();
				});
				Assert.That(container.Get<ContainerClass1>().outerClass.innerClass.@interface, Is.InstanceOf<Impl1>());
				Assert.That(container.Get<ContainerClass2>().outerClass.innerClass.@interface, Is.InstanceOf<Impl2>());
			}
		}

		public class ContractsWorksWithFactories : ContractsTest
		{
			public class A
			{
				public readonly B bc1;
				public readonly B bc2;

				public A([RequireContract("c1")] B bc1, [RequireContract("c2")] B bc2)
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

		public class ContractsWorksWithFactoriesWithArguments : ContractsTest
		{
			public class A
			{
				public readonly B bc1;
				public readonly B bc2;

				public A([RequireContract("c1")] B bc1, [RequireContract("c2")] B bc2)
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
				public IInterface intf { get; set; }
				public int value { get; set; }

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

		public class DoNotDuplicateServicesNotDependentOnContracts : ContractsTest
		{
			public class ServiceClient1
			{
				public readonly Service service;
				public readonly SingletonService singletonService;

				public ServiceClient1([RequireContract("c1")] Service service, SingletonService singletonService)
				{
					this.service = service;
					this.singletonService = singletonService;
				}
			}

			public class ServiceClient2
			{
				public readonly Service service;
				public SingletonService singletonService;

				public ServiceClient2([RequireContract("c2")] Service service, SingletonService singletonService)
				{
					this.service = service;
					this.singletonService = singletonService;
				}
			}

			public class SingletonService
			{
			}

			public class Service
			{
				public readonly SingletonService singletonService;

				public Service(IInterface @interface, SingletonService singletonService)
				{
					this.singletonService = singletonService;
				}
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

			[Test]
			public void Test()
			{
				var container = Container(c =>
				{
					c.Contract("c1").Bind<IInterface, Impl1>();
					c.Contract("c2").Bind<IInterface, Impl2>();
				});
				var singletonServiceInstance = container.Get<ServiceClient1>().service.singletonService;
				Assert.That(container.Get<ServiceClient2>().service.singletonService, Is.SameAs(singletonServiceInstance));
				Assert.That(container.Get<ServiceClient1>().singletonService, Is.SameAs(singletonServiceInstance));
				Assert.That(container.Get<ServiceClient2>().singletonService, Is.SameAs(singletonServiceInstance));
			}
		}

		public class NestedContractsAreProhibited : ContractsTest
		{
			public class Wrap
			{
				public Wrap(OuterService outerService)
				{
				}
			}

			public class OuterService
			{
				public OuterService([RequireContract("c1")] InnerService innerService)
				{
				}
			}

			public class InnerService
			{
				public InnerService([RequireContract("c2")] Service service)
				{
				}
			}

			public class Service
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("c1").Bind<InnerService, InnerService>();
					builder.Contract("c2").Bind<Service, Service>();
				});
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<Wrap>());
				const string expectedMessage =
					"nested contexts are not supported, outer contract [c1], inner contract [c2]\r\n" +
					"Wrap!\r\n\tOuterService!\r\n\t\tInnerService[c1]->[c1]!";
				Assert.That(error.Message, Is.EqualTo(expectedMessage));
			}
		}

		public class ContractFormatting : ContractsTest
		{
			public class Wrap
			{
				public Wrap([RequireContract("c1")] Service service)
				{
				}
			}

			public class Service
			{
				public Service(SingletonService singletonService, IInterface @interface)
				{
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
				public Impl1(IUnimplemented unimplemented)
				{
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(c => c.Contract("c1").Bind<IInterface, Impl1>());
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<Wrap>());
				const string expectedMessage =
					"no implementations for Wrap\r\nWrap!\r\n\tService[c1]->[c1]!\r\n\t\tSingletonService\r\n\t\tIInterface[c1]!\r\n\t\t\tImpl1!\r\n\t\t\t\tIUnimplemented! - has no implementations";
				Assert.That(error.Message, Is.EqualTo(expectedMessage));
			}
		}

		public class ContractUsageViaCache : ContractsTest
		{
			public class Client
			{
				public readonly ServiceWrap wrap;
				public readonly OtherService otherService;

				public Client([RequireContract("c1")] ServiceWrap wrap, OtherService otherService)
				{
					this.wrap = wrap;
					this.otherService = otherService;
				}
			}

			public class ServiceWrap
			{
				public readonly Service service;
				public readonly OtherService otherService;

				public ServiceWrap(Service service, OtherService otherService)
				{
					this.service = service;
					this.otherService = otherService;
				}
			}

			public class OtherService
			{
				private readonly Service service;

				public OtherService(Service service)
				{
					this.service = service;
				}
			}

			public class Service
			{
				public Service(IInterface @interface)
				{
				}
			}

			public interface IInterface
			{
			}

			public class Impl : IInterface
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(c => c.Contract("c1").Bind<IInterface, Impl>());
				var client = container.Get<Client>();
				Assert.That(client.wrap.otherService, Is.Not.SameAs(client.otherService));
			}
		}

		public class SelfConfigurationContractDependency : ContractsTest
		{
			public class Wrap
			{
				public readonly X first;
				public readonly X second;

				public Wrap([RequireContract("first")] X first, [RequireContract("second")] X second)
				{
					this.first = first;
					this.second = second;
				}
			}

			public class X
			{
				public readonly IIntf intf;

				public X(IIntf intf)
				{
					this.intf = intf;
				}
			}

			public interface IIntf
			{
			}

			public class Y : IIntf
			{
			}

			public class Z : IIntf
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("first").BindDependency<X, IIntf, Y>();
					builder.Contract("second").BindDependency<X, IIntf, Z>();
				});
				var wrap = container.Get<Wrap>();
				Assert.That(wrap.first.intf, Is.InstanceOf<Y>());
				Assert.That(wrap.second.intf, Is.InstanceOf<Z>());
			}
		}

		public class ContractsCanBeUnioned : ContractsTest
		{
			public class AllWrapsHost
			{
				public readonly ServiceWrap[] wraps;

				public AllWrapsHost([RequireContract("composite-contract")] IEnumerable<ServiceWrap> wraps)
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

		public class ContractIsNotConfigured : ContractsTest
		{
			public class Service
			{
				public Service([RequireContract("some-contract")] Dependency dependency)
				{
				}
			}

			public class Dependency
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var e = Assert.Throws<SimpleContainerException>(() => container.Get<Service>());
				Assert.That(e.Message, Is.EqualTo("contract [some-contract] is not configured\r\nService!"));
			}
		}

		public class UnionContractsWithNonContractDependentServices : ContractsTest
		{
			public class ServiceWrap
			{
				public readonly Service[] wraps;

				public ServiceWrap([RequireContract("composite-contract")] IEnumerable<Service> wraps)
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

		public class ContractCanBeSpecifiedViaInfection : ContractsTest
		{
			public class Wrap
			{
				public readonly IService serviceA;
				public readonly IService serviceB;

				public Wrap([RequireContract("A")] IService serviceA, [RequireContract("B")] IService serviceB)
				{
					this.serviceA = serviceA;
					this.serviceB = serviceB;
				}
			}

			public interface IService
			{
			}

			public class ServiceA : IService
			{
			}

			public class ServiceB : IService
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("A").Bind<IService, ServiceA>();
					builder.Contract("B").Bind<IService, ServiceB>();
				});
				var wrap = container.Get<Wrap>();
				Assert.That(wrap.serviceA, Is.InstanceOf<ServiceA>());
				Assert.That(wrap.serviceB, Is.InstanceOf<ServiceB>());
			}
		}

		public class CanAttachContractOnClass : ContractsTest
		{
			public class Wrap
			{
				public readonly Service service;

				public Wrap(Service service)
				{
					this.service = service;
				}
			}

			public class TestContractAttribute : RequireContractAttribute
			{
				public TestContractAttribute()
					: base("test-contract")
				{
				}
			}

			[TestContract]
			public class Service
			{
				public readonly IInterface @interface;

				public Service(IInterface @interface)
				{
					this.@interface = @interface;
				}
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

			[Test]
			public void Test()
			{
				var container = Container(b => b.Contract<TestContractAttribute>().Bind<IInterface, Impl2>());
				var wrap = container.Get<Wrap>();
				Assert.That(wrap.service.@interface, Is.InstanceOf<Impl2>());
			}
		}

		public class ContractAttachedOnClassIsUsedOnGet : ContractsTest
		{
			[RequireContract("a")]
			public class A
			{
				public readonly ISomeInterface someInterface;

				public A(ISomeInterface someInterface)
				{
					this.someInterface = someInterface;
				}
			}

			public interface ISomeInterface
			{
			}

			public class Impl1 : ISomeInterface
			{
			}

			public class Impl2 : ISomeInterface
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.Contract("a").Bind<ISomeInterface, Impl2>());
				Assert.That(container.Get<A>().someInterface, Is.InstanceOf<Impl2>());
			}
		}
	}
}