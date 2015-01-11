using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Implementation;
using SimpleContainer.Infection;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class ContractsTest : SimpleContainerTestBase
	{
		public class RequireContractTest : ContractsTest
		{
			public class ContainerClass1
			{
				public readonly OuterClass outerClass;

				public ContainerClass1([TestContract("c1")] OuterClass outerClass)
				{
					this.outerClass = outerClass;
				}
			}

			public class ContainerClass2
			{
				public readonly OuterClass outerClass;

				public ContainerClass2([TestContract("c2")] OuterClass outerClass)
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

		public class ContractsWorksWithFactoriesWithArguments : ContractsTest
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

				public ServiceClient1([TestContract("c1")] Service service, SingletonService singletonService)
				{
					this.service = service;
					this.singletonService = singletonService;
				}
			}

			public class ServiceClient2
			{
				public readonly Service service;
				public SingletonService singletonService;

				public ServiceClient2([TestContract("c2")] Service service, SingletonService singletonService)
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
				public Wrap([TestContract("c1")] Service service)
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

				public Wrap([TestContract("a1")] A a1, [TestContract("a2")] A a2)
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
				Assert.That(container.GetConstructionLog(typeof (A), "a2"), Is.EqualTo("A->[a2] - reused"));
			}
		}

		public class ContractUsageViaCache : ContractsTest
		{
			public class Client
			{
				public readonly ServiceWrap wrap;
				public readonly OtherService otherService;

				public Client([TestContract("c1")] ServiceWrap wrap, OtherService otherService)
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

				public Wrap([TestContract("first")] X first, [TestContract("second")] X second)
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

		public class UnionContractsWithNonContractDependentServices : ContractsTest
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

		public class ContractCanBeSpecifiedViaInfection : ContractsTest
		{
			public class Wrap
			{
				public readonly IService serviceA;
				public readonly IService serviceB;

				public Wrap([TestContract("A")] IService serviceA, [TestContract("B")] IService serviceB)
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

				public A([TestContract("x")] Wrap wrap, [TestContract("x")] C c1, C c2)
				{
					this.wrap = wrap;
					this.c1 = c1;
					this.c2 = c2;
				}
			}

			[TestContract("not-used")]
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
				Assert.That(container.GetConstructionLog(typeof (Service)),
					Is.EqualTo("Service[test-contract]->[test-contract]\r\n\tIInterface[test-contract]\r\n\t\tImpl2"));
			}
		}

		public class ContractAttachedOnClassIsUsedOnGet : ContractsTest
		{
			[TestContract("a")]
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

				public Wrap([TestContract("a1")] IInterface1 s1, [TestContract("a2")] IInterface1 s2)
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

				public Impl1([TestContract("b1")] IInterface2 s1, [TestContract("b2")] IInterface2 s2)
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

				public Wrap([TestContract("a")] A a)
				{
					this.a = a;
				}
			}

			[TestContract("b")]
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

				public H([TestContract("x")] A a1, A a2)
				{
					this.a1 = a1;
					this.a2 = a2;
				}
			}

			public class A
			{
				public readonly B b;

				public A([TestContract("y")] B b)
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

				public A([TestContract("a")] Wrap s1, Wrap s2)
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

				public A([TestContract("x")] B b, [TestContract("y")] C c)
				{
					this.b = b;
					this.c = c;
				}
			}

			[TestContract("not-used")]
			public class B
			{
				public readonly U u;

				public B([TestContract("y")] U u)
				{
					this.u = u;
				}
			}

			public class C
			{
				public readonly U u;

				public C([TestContract("x")] U u)
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

				public A([TestContract("x")] B b)
				{
					this.b = b;
				}
			}

			[TestContract("x")]
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

				public Wrap([TestContract("b")] A a)
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

		public class ConstructionLogForFactory : ContractsTest
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
				container.Get<A>();
				Assert.That(container.GetConstructionLog(typeof (A)), Is.EqualTo("A[a]->[a]\r\n\tFunc<B>[a]"));
			}
		}

		public class DumpUsedContractsBeforeFinalConstruction : ContractsTest
		{
			[TestContract("a")]
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
				var container = Container(b => b.Contract("a").BindDependency<A>("parameter", 78));
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<A>());
				Assert.That(error.Message,
					Is.StringContaining("A[a]->[a]!\r\n\tparameter -> 78\r\n\tB!\r\n\t\tparameter! - <---------------"));
			}
		}

		public class FinalUsedContractsForSkippedServices : ContractsTest
		{
			public class Wrap
			{
				public readonly A a;

				public Wrap([TestContract("x")] A a)
				{
					this.a = a;
				}
			}

			public class A
			{
				public readonly B b;

				public A([Optional] B b)
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
				var container = Container(b => b.Contract("x").DontUse<B>());
				Assert.That(container.Get<Wrap>().a.b, Is.Null);
				Assert.That(container.GetConstructionLog(typeof (A), "x"), Is.EqualTo("A->[x]\r\n\tB[x]! - DontUse"));
			}
		}

		public class ExplicitlyCommentReuseFromUsedContracts : ContractsTest
		{
			public class A
			{
				public readonly B b1;
				public readonly B b2;

				public A([TestContract("x")] B b1, [TestContract("y")] B b2)
				{
					this.b1 = b1;
					this.b2 = b2;
				}
			}

			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(b =>
				{
					b.Contract("x");
					b.Contract("y");
				});
				var a = container.Get<A>();
				Assert.That(a.b1, Is.SameAs(a.b2));
				Assert.That(container.GetConstructionLog(typeof (A)), Is.EqualTo("A\r\n\tB->[x]\r\n\tB->[y] - reused"));
			}
		}

		public class NestedRequiredContracts : ContractsTest
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
			}
		}

		public class RequiredUnionedContracts : ContractsTest
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
				Assert.That(a.b.enumerable.Select(x => x.parameter).ToArray(), Is.EquivalentTo(new[] {1, 2}));
				Assert.That(a.y1C.parameter, Is.EqualTo(3));
				Assert.That(a.y2C.parameter, Is.EqualTo(4));
			}
		}

		public class ManyRequiredAndUnionedContracts : ContractsTest
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

		public class CrashInConstructorOfServiceForUsedContracts : ContractsTest
		{
			public class A
			{
				public A()
				{
					throw new InvalidOperationException("test crash");
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.Contract("a"));
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<A>("a"));
				Assert.That(error.Message, Is.StringContaining("construction exception"));
				Assert.That(error.InnerException.Message, Is.EqualTo("test crash"));
			}
		}

		public class CanUseContractOnContractConfiguratorForRestriction : ContractsTest
		{
			public class Contract1Attribute : RequireContractAttribute
			{
				public Contract1Attribute() : base("contract-1")
				{
				}
			}

			public class Contract2Attribute : RequireContractAttribute
			{
				public Contract2Attribute() : base("contract-2")
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

			public class CConfigurator:IServiceConfigurator<C>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<C> builder)
				{
					builder.Contract<Contract1Attribute>().Contract<Contract2Attribute>().Dependencies(new {parameter = 1});
					builder.Contract<Contract2Attribute>().Dependencies(new {parameter = 2});
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

		public class NotDefinedContractsCanBeUsedLaterToRestrictOtherContracts : ContractsTest
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

		public class ServicesAreBoundToUsedContractPath : ContractsTest
		{
			public class A
			{
				public readonly B b;
				public readonly C c;

				public A([TestContract("x1")] B b, [TestContract("x2")] C c)
				{
					this.b = b;
					this.c = c;
				}
			}

			public class B
			{
				public readonly C c;

				public B([TestContract("x2")] C c)
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