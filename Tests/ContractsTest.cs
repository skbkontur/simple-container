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

		public class ConstructionLogForSpecificContract : ContractsTest
		{
			public class Wrap
			{
				public readonly A a1;
				public readonly A a2;

				public Wrap([RequireContract("a1")] A a1, [RequireContract("a2")] A a2)
				{
					this.a1 = a1;
					this.a2 = a2;
				}
			}

			public class A
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("a1");
					builder.Contract("a2");
				});
				container.Get<Wrap>();
				Assert.That(container.GetConstructionLog(typeof (A), "a2"), Is.EqualTo("A->[a2]"));
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

		public class UsedContractsForServiceCreatedUsingFinalContracts : ContractsTest
		{
			public class A
			{
				public readonly Wrap wrap;
				public readonly C c1;
				public readonly C c2;

				public A([RequireContract("x")] Wrap wrap, [RequireContract("x")] C c1, C c2)
				{
					this.wrap = wrap;
					this.c1 = c1;
					this.c2 = c2;
				}
			}

			[RequireContract("not-used")]
			public class Wrap
			{
				public readonly B b;

				public Wrap(B b)
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
				public readonly B b;

				public C(B b)
				{
					this.b = b;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("not-used");
					builder.BindDependency<B>("parameter", 1);
					builder.Contract("x").BindDependency<B>("parameter", 2);
				});
				var a = container.Get<A>();
				Assert.That(a.wrap.b.parameter, Is.EqualTo(2));
				Assert.That(a.c1.b.parameter, Is.EqualTo(2));
				Assert.That(a.c2.b.parameter, Is.EqualTo(1));
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

		public class SimpleNestedContracts : ContractsTest
		{
			public class Wrap
			{
				public readonly IInterface1 s1;
				public readonly IInterface1 s2;

				public Wrap([RequireContract("a1")] IInterface1 s1, [RequireContract("a2")] IInterface1 s2)
				{
					this.s1 = s1;
					this.s2 = s2;
				}
			}

			public interface IInterface1
			{
			}

			public class Impl1 : IInterface1
			{
				public readonly IInterface2 s1;
				public readonly IInterface2 s2;

				public Impl1([RequireContract("b1")] IInterface2 s1, [RequireContract("b2")] IInterface2 s2)
				{
					this.s1 = s1;
					this.s2 = s2;
				}
			}

			public class Impl2 : IInterface1
			{
			}

			public interface IInterface2
			{
			}

			public class Impl3 : IInterface2
			{
			}

			public class Impl4 : IInterface2
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("a1").Bind<IInterface1, Impl1>();
					builder.Contract("a2").Bind<IInterface1, Impl2>();
					builder.Contract("b1").Bind<IInterface2, Impl3>();
					builder.Contract("b2").Bind<IInterface2, Impl4>();
				});
				var instance = container.Get<Wrap>();
				Assert.That(instance.s1, Is.InstanceOf<Impl1>());
				Assert.That(instance.s2, Is.InstanceOf<Impl2>());

				var impl = (Impl1) instance.s1;
				Assert.That(impl.s1, Is.InstanceOf<Impl3>());
				Assert.That(impl.s2, Is.InstanceOf<Impl4>());
			}
		}

		public class ContractOnClassAndParameter : ContractsTest
		{
			public class Wrap
			{
				public readonly A a;

				public Wrap([RequireContract("a")] A a)
				{
					this.a = a;
				}
			}

			[RequireContract("b")]
			public class A
			{
				public readonly B b;

				public A(B b)
				{
					this.b = b;
				}
			}

			public class B
			{
				public readonly int dependency;
				public readonly C c;

				public B(int dependency, C c)
				{
					this.dependency = dependency;
					this.c = c;
				}
			}

			public class C
			{
				public readonly int dependency;

				public C(int dependency)
				{
					this.dependency = dependency;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("a").BindDependency<B>("dependency", 1);
					builder.Contract("b").BindDependency<C>("dependency", 2);
				});
				var wrap = container.Get<Wrap>();
				Assert.That(wrap.a.b.dependency, Is.EqualTo(1));
				Assert.That(wrap.a.b.c.dependency, Is.EqualTo(2));
			}
		}

		public class ContractsFlowViaDependenciesWithRequireContract : ContractsTest
		{
			public class H
			{
				public readonly A a1;
				public readonly A a2;

				public H([RequireContract("x")] A a1, A a2)
				{
					this.a1 = a1;
					this.a2 = a2;
				}
			}

			public class A
			{
				public readonly B b;

				public A([RequireContract("y")] B b)
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

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("y");
					builder.BindDependency<B>("parameter", 1);
					builder.Contract("x").BindDependency<B>("parameter", 2);
				});
				var h = container.Get<H>();
				Assert.That(h.a1.b.parameter, Is.EqualTo(2));
				Assert.That(h.a2.b.parameter, Is.EqualTo(1));
			}
		}

		public class ContractsFlowViaInterfaces : ContractsTest
		{
			public class A
			{
				public readonly Wrap s1;
				public readonly Wrap s2;

				public A([RequireContract("a")] Wrap s1, Wrap s2)
				{
					this.s1 = s1;
					this.s2 = s2;
				}
			}

			public class Wrap
			{
				public readonly IInterface wrapped;

				public Wrap(IInterface wrapped)
				{
					this.wrapped = wrapped;
				}
			}

			public interface IInterface
			{
				int GetParameter();
			}

			public class Impl1 : IInterface
			{
				public readonly int parameter;

				public Impl1(int parameter)
				{
					this.parameter = parameter;
				}

				public int GetParameter()
				{
					return parameter;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.BindDependency<Impl1>("parameter", 1);
					builder.Contract("a").BindDependency<Impl1>("parameter", 2);
				});
				var a = container.Get<A>();
				Assert.That(a.s1.wrapped.GetParameter(), Is.EqualTo(2));
				Assert.That(a.s2.wrapped.GetParameter(), Is.EqualTo(1));
			}
		}

		public class ContractsSequenceIsImportant : ContractsTest
		{
			public class A
			{
				public readonly B b;
				public readonly C c;

				public A([RequireContract("x")] B b, [RequireContract("y")] C c)
				{
					this.b = b;
					this.c = c;
				}
			}

			[RequireContract("not-used")]
			public class B
			{
				public readonly U u;

				public B([RequireContract("y")] U u)
				{
					this.u = u;
				}
			}

			public class C
			{
				public readonly U u;

				public C([RequireContract("x")] U u)
				{
					this.u = u;
				}
			}

			public class U
			{
				public readonly IInterface s;

				public U(IInterface s)
				{
					this.s = s;
				}
			}

			public interface IInterface
			{
			}

			public class Impl1 : IInterface
			{
				public readonly int parameter;

				public Impl1(int parameter)
				{
					this.parameter = parameter;
				}
			}

			public class Impl2 : IInterface
			{
				public readonly int parameter;

				public Impl2(int parameter)
				{
					this.parameter = parameter;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("not-used");
					builder.Contract("x")
						.Bind<IInterface, Impl2>()
						.BindDependency<Impl1>("parameter", 1);

					builder.Contract("y")
						.BindDependency<Impl2>("parameter", 2)
						.Bind<IInterface, Impl1>();
				});

				var a = container.Get<A>();
				Assert.That(a.b.u.s, Is.InstanceOf<Impl1>());
				Assert.That(((Impl1) a.b.u.s).parameter, Is.EqualTo(1));

				Assert.That(a.c.u.s, Is.InstanceOf<Impl2>());
				Assert.That(((Impl2) a.c.u.s).parameter, Is.EqualTo(2));
			}
		}

		public class ProhibitDuplicatedContractInChain : ContractsTest
		{
			public class A
			{
				public readonly B b;

				public A([RequireContract("x")] B b)
				{
					this.b = b;
				}
			}

			[RequireContract("x")]
			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.Contract("x"));
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<A>());
				Assert.That(error.Message, Is.EqualTo("contract [x] already required, all required contracts [x]\r\nA!"));
			}
		}

		public class ContractUsedInEnumerableDependency : ContractsTest
		{
			public class Wrap
			{
				public readonly A a;

				public Wrap([RequireContract("b")] A a)
				{
					this.a = a;
				}
			}

			public class A
			{
				public readonly IEnumerable<B> enumerable;

				public A(IEnumerable<B> enumerable)
				{
					this.enumerable = enumerable;
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
				var container = Container(b => b.Contract("b").BindDependency<B>("parameter", 128));
				Assert.That(container.Get<Wrap>().a.enumerable.Single().parameter, Is.EqualTo(128));
			}
		}

		public class ServicesAreBoundToUsedContractPath : ContractsTest
		{
			public class A
			{
				public readonly B b;
				public readonly C c;

				public A([RequireContract("x1")] B b, [RequireContract("x2")] C c)
				{
					this.b = b;
					this.c = c;
				}
			}

			public class B
			{
				public readonly C c;

				public B([RequireContract("x2")] C c)
				{
					this.c = c;
				}
			}

			public class C
			{
				public readonly int p;

				public C(int p)
				{
					this.p = p;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b =>
				{
					b.Contract("x1");
					b.Contract("x2").BindDependency<C>("p", 42);
				});
				var a = container.Get<A>();
				Assert.That(a.b.c, Is.SameAs(a.c));
			}
		}
	}
}