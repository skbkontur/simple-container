using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Infection;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.Contracts
{
	public abstract class ContractsBasicTest : SimpleContainerTestBase
	{
		public class RequireTest : ContractsBasicTest
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

		public class DoNotDuplicateServicesNotDependentOn : ContractsBasicTest
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
				public readonly IInterface @interface;
				public readonly SingletonService singletonService;

				public Service(IInterface @interface, SingletonService singletonService)
				{
					this.@interface = @interface;
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

		public class SelfConfigurationDependency : ContractsBasicTest
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

		public class CanBeSpecifiedViaInfection : ContractsBasicTest
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

		public class IgnoreNotDeclared : ContractsBasicTest
		{
			public class A
			{
				public readonly B b;

				public A(B b)
				{
					this.b = b;
				}
			}

			[TestContract("not-declared")]
			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.DoesNotThrow(() => container.Get<A>());
			}
		}

		public class ClassIsAppliedAfterDependency : ContractsBasicTest
		{
			public class A
			{
				public readonly B b;

				public A([TestContract("c1")] B b)
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
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("c1").BindDependency<B>("parameter", 42);
					builder.Contract("c2").BindDependency<B>("parameter", 43);
				});
				Assert.That(container.Get<A>().b.parameter, Is.EqualTo(43));
			}
		}

		public class CanAttachContractOnClass : ContractsBasicTest
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
				Assert.That(container.Resolve<Service>().GetConstructionLog(),
					Is.EqualTo("Service[test-contract]"
						+ Environment.NewLine + "\tIInterface[test-contract]"
						+ Environment.NewLine + "\t\tImpl2"));
			}
		}

		public class ContractAttachedOnClassIsUsedOnGet : ContractsBasicTest
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

		public class SimpleNestedContracts : ContractsBasicTest
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

		public class ContractOnClassAndParameter : ContractsBasicTest
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

		public class ContractsFlowViaDependenciesWithRequireContract : ContractsBasicTest
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

		public class ContractsFlowViaInterfaces : ContractsBasicTest
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

		public class ContractUsedInEnumerableDependency : ContractsBasicTest
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

		public class DumpUsedContractsBeforeFinalConstruction : ContractsBasicTest
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
					Does.Contain("!A[a]"
						+ Environment.NewLine + "\tparameter -> 78"
						+ Environment.NewLine + "\t!B"
						+ Environment.NewLine + "\t\t!parameter <---------------"));
			}
		}

		public class ServiceWithContractImplementsInterface : ContractsBasicTest
		{
			public interface IA
			{
			}

			[TestContract("x")]
			public class A : IA
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
				var container = Container(b => b.Contract("x").BindDependency<A>("parameter", 42));
				var a = (A) container.Get<IA>();
				Assert.That(a.parameter, Is.EqualTo(42));
			}
		}

		public class CanInjectServiceName : ContractsBasicTest
		{
			public class A
			{
				public readonly B b;

				public A([TestContract("my-test-contract")] B b)
				{
					this.b = b;
				}
			}

			public class B
			{
				public readonly int parameter;
				public readonly ServiceName myName1;
				public readonly ServiceName myName2;

				public B(ServiceName myName1, int parameter, ServiceName myName2)
				{
					this.myName1 = myName1;
					this.parameter = parameter;
					this.myName2 = myName2;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.Contract("my-test-contract").BindDependencies<B>(new {parameter = 78}));
				var instance = container.Get<A>().b;
				Assert.That(instance.myName1.ToString(), Is.EqualTo("B[my-test-contract]"));
				Assert.That(instance.myName2.ToString(), Is.EqualTo("B[my-test-contract]"));
			}
		}

		public class CanOverrideServiceInSpecificContract : ContractsBasicTest
		{
			public class A
			{
				public readonly B b1;
				public readonly B b2;

				public A(B b1, [TestContract("c")] B b2)
				{
					this.b1 = b1;
					this.b2 = b2;
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
					b.Contract("c").BindDependency<B>("parameter", 2);
				});

				var instance = container.Get<A>();
				Assert.That(instance.b1.parameter, Is.EqualTo(1));
				Assert.That(instance.b2.parameter, Is.EqualTo(2));

				using (var child = container.Clone(b => b.BindDependency<B>("parameter", 3)))
				{
					var childInstance = child.Get<A>();
					Assert.That(childInstance.b1.parameter, Is.EqualTo(3));
					Assert.That(childInstance.b2.parameter, Is.EqualTo(2));
				}
			}
		}

		public class ContractRedefinitionIsProhibited : ContractsBasicTest
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
				Assert.That(error.Message,
					Is.EqualTo("contract [x] already declared, stack"
						+ Environment.NewLine + "\tA"
						+ Environment.NewLine + "\tB[x->x]"
						+ Environment.NewLine
						+ Environment.NewLine + "!A"
						+ Environment.NewLine + "\t!B <---------------"));
			}
		}

		public class SameInterfaceInContractNotACyclicDependency : ContractsBasicTest
		{
			public interface IB
			{
			}

			public class ServiceB : IB
			{
				public C C;

				public ServiceB([TestContract("test")] C c)
				{
					C = c;
				}
			}

			public class InternalB : IB
			{
			}

			public class C
			{
				public IB B;

				public C(IB b)
				{
					B = b;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(x =>
				{
					x.Bind<IB, ServiceB>();
					x.Contract("test").Bind<IB, InternalB>();
				});
				var serviceB = (ServiceB) container.Get<IB>();
				Assert.That(serviceB.C.B, Is.SameAs(container.Get<InternalB>()));
			}
		}
	}
}