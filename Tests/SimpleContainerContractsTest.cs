using System;
using NUnit.Framework;

namespace SimpleContainer.Tests
{
	public abstract class SimpleContainerContractsTest : SimpleContainerTestBase
	{
		public class RequireContractTest : SimpleContainerContractsTest
		{
			public class ContainerClass1
			{
				public readonly OuterClass outerClass;

				public ContainerClass1(OuterClass outerClass)
				{
					this.outerClass = outerClass;
				}
			}

			public class ContainerClass2
			{
				public readonly OuterClass outerClass;

				public ContainerClass2(OuterClass outerClass)
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
					c.InContext<ContainerClass1>("outerClass").Bind<IInterface, Impl1>();
					c.InContext<ContainerClass2>("outerClass").Bind<IInterface, Impl2>();
				});
				Assert.That(container.Get<ContainerClass1>().outerClass.innerClass.@interface, Is.InstanceOf<Impl1>());
				Assert.That(container.Get<ContainerClass2>().outerClass.innerClass.@interface, Is.InstanceOf<Impl2>());
			}
		}

		public class ContractsWorksWithFactories : SimpleContainerContractsTest
		{
			public class A
			{
				public readonly B bc1;
				public readonly B bc2;

				public A(B bc1, B bc2)
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
					builder.InContext<A>("bc1").Bind<IInterface, C>();
					builder.InContext<A>("bc2").Bind<IInterface, D>();
				});
				var a = container.Get<A>();
				Assert.That(a.bc1.getInterface(), Is.InstanceOf<C>());
				Assert.That(a.bc2.getInterface(), Is.InstanceOf<D>());
			}
		}

		public class ContractsWorksWithFactoriesWithArguments : SimpleContainerContractsTest
		{
			public class A
			{
				public readonly B bc1;
				public readonly B bc2;

				public A(B bc1, B bc2)
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
					builder.InContext<A>("bc1").Bind<IInterface, C>();
					builder.InContext<A>("bc2").Bind<IInterface, D>();
				});
				var a = container.Get<A>();
				var result1 = a.bc1.getResult(new { value = 1 });
				var result2 = a.bc2.getResult(new { value = 2 });
				Assert.That(result1.value, Is.EqualTo(1));
				Assert.That(result2.value, Is.EqualTo(2));
				Assert.That(result1.intf, Is.InstanceOf<C>());
				Assert.That(result2.intf, Is.InstanceOf<D>());
			}
		}

		public class DoNotDuplicateServicesNotDependentOnContracts : SimpleContainerContractsTest
		{
			public class ServiceClient1
			{
				public readonly Service service;
				public readonly SingletonService singletonService;

				public ServiceClient1(Service service, SingletonService singletonService)
				{
					this.service = service;
					this.singletonService = singletonService;
				}
			}

			public class ServiceClient2
			{
				public readonly Service service;
				public SingletonService singletonService;

				public ServiceClient2(Service service, SingletonService singletonService)
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
					c.InContext<ServiceClient1>("service").Bind<IInterface, Impl1>();
					c.InContext<ServiceClient2>("service").Bind<IInterface, Impl2>();
				});
				var singletonServiceInstance = container.Get<ServiceClient1>().service.singletonService;
				Assert.That(container.Get<ServiceClient2>().service.singletonService, Is.SameAs(singletonServiceInstance));
				Assert.That(container.Get<ServiceClient1>().singletonService, Is.SameAs(singletonServiceInstance));
				Assert.That(container.Get<ServiceClient2>().singletonService, Is.SameAs(singletonServiceInstance));
			}
		}

		public class NestedContractsAreProhibited : SimpleContainerContractsTest
		{
			public class Wrap
			{
				public Wrap(OuterService outerService)
				{
				}
			}

			public class OuterService
			{
				public OuterService(InnerService innerService)
				{
				}
			}

			public class InnerService
			{
				public InnerService(Service service)
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
					builder.InContext<OuterService>("innerService").Bind<InnerService, InnerService>();
					builder.InContext<InnerService>("service").Bind<Service, Service>();
				});
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<Wrap>());
				const string expectedMessage = "nested contexts are not supported, outer context [OuterService.innerService], inner context [InnerService.service]\r\n" +
											   "Wrap!\r\n\tOuterService!\r\n\t\tInnerService[OuterService.innerService]->[OuterService.innerService]!";
				Assert.That(error.Message, Is.EqualTo(expectedMessage));
			}
		}

		public class ContractFormatting : SimpleContainerContractsTest
		{
			public class Wrap
			{
				public Wrap(Service service)
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
				var container = Container(c => c.InContext<Wrap>("service").Bind<IInterface, Impl1>());
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<Wrap>());
				const string expectedMessage =
					"no implementations for Wrap\r\nWrap!\r\n\tService[Wrap.service]->[Wrap.service]!\r\n\t\tSingletonService\r\n\t\tIInterface[Wrap.service]!\r\n\t\t\tImpl1!\r\n\t\t\t\tIUnimplemented!";
				Assert.That(error.Message, Is.EqualTo(expectedMessage));
			}
		}

		public class ContractUsageViaCache : SimpleContainerContractsTest
		{
			public class Client
			{
				public readonly ServiceWrap wrap;
				public readonly OtherService otherService;

				public Client(ServiceWrap wrap, OtherService otherService)
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
				var container = Container(c => c.InContext<Client>("wrap").Bind<IInterface, Impl>());
				var client = container.Get<Client>();
				Assert.That(client.wrap.otherService, Is.Not.SameAs(client.otherService));
			}
		}

		public class SelfConfigurationContractDependency : SimpleContainerContractsTest
		{
			public class Wrap
			{
				public readonly X first;
				public readonly X second;

				public Wrap(X first, X second)
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
					builder.InContext<Wrap>("first").BindDependency<X, IIntf, Y>();
					builder.InContext<Wrap>("second").BindDependency<X, IIntf, Z>();
				});
				var wrap = container.Get<Wrap>();
				Assert.That(wrap.first.intf, Is.InstanceOf<Y>());
				Assert.That(wrap.second.intf, Is.InstanceOf<Z>());
			}
		}
	}
}
