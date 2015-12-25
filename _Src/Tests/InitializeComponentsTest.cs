using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Infection;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class InitializeComponentsTest : SimpleContainerTestBase
	{
		public class Simple : InitializeComponentsTest
		{
			public class ComponentWrap
			{
				public readonly Component2 component2;

				public ComponentWrap(Component2 component2)
				{
					this.component2 = component2;
				}
			}

			public class Component2 : IInitializable
			{
				public readonly IntermediateService intermediateService;

				public Component2(IntermediateService intermediateService)
				{
					this.intermediateService = intermediateService;
					LogBuilder.Append("Component2.ctor ");
				}

				public void Initialize()
				{
					LogBuilder.Append("Component2.Initialize ");
				}
			}

			public class IntermediateService
			{
				public Component1 component1;

				public IntermediateService(Component1 component1)
				{
					this.component1 = component1;
					LogBuilder.Append("IntermediateService.ctor ");
				}
			}

			public class Component1 : IInitializable
			{
				public Component0 component0;

				public Component1(Component0 component0)
				{
					this.component0 = component0;
					LogBuilder.Append("Component1.ctor ");
				}

				public void Initialize()
				{
					LogBuilder.Append("Component1.Initialize ");
				}
			}

			public class Component0 : IInitializable
			{
				public Component0()
				{
					LogBuilder.Append("Component0.ctor ");
				}

				public void Initialize()
				{
					LogBuilder.Append("Component0.Initialize ");
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				container.Get<ComponentWrap>();
				const string constructorsLog = "Component0.ctor Component1.ctor IntermediateService.ctor Component2.ctor ";
				const string runLog = "Component0.Initialize Component1.Initialize Component2.Initialize ";
				Assert.That(LogBuilder.ToString(), Is.EqualTo(constructorsLog + runLog));
			}
		}

		public class InitializeComponentsFromCache : InitializeComponentsTest
		{
			public class A
			{
				public readonly B b;
				public readonly C c;

				public A([Optional] B b, C c)
				{
					this.b = b;
					this.c = c;
				}
			}

			public class B
			{
				public readonly C c;
				public readonly D d;

				public B(C c, D d)
				{
					this.c = c;
					this.d = d;
				}
			}

			public class C : IInitializable
			{
				public static bool initializeCalled;

				public void Initialize()
				{
					initializeCalled = true;
				}
			}

			public class D
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.DontUse<D>());
				container.Resolve<A>().EnsureInitialized();
				Assert.That(C.initializeCalled);
			}
		}

		public class DoNotInitializeNotUsedComponents : InitializeComponentsTest
		{
			private static readonly StringBuilder logBuilder = new StringBuilder();

			public class A
			{
				public readonly BWrap b;

				public A([Optional] BWrap b)
				{
					this.b = b;
				}
			}

			public class BWrap
			{
				public readonly B b;
				public readonly FilteredService filteredService;

				public BWrap(B b, FilteredService filteredService)
				{
					this.b = b;
					this.filteredService = filteredService;
				}
			}

			public class FilteredService
			{
			}

			public class B : IInitializable
			{
				public void Initialize()
				{
					logBuilder.Append("Initialize ");
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.WithInstanceFilter<FilteredService>(x => false));
				var a = container.Get<A>();
				Assert.That(a.b, Is.Null);
				Assert.That(logBuilder.ToString(), Is.Empty);
			}
		}

		public class InitializeUsesInstancesReachableFromAllResolutionContexts : InitializeComponentsTest
		{
			private static readonly StringBuilder logBuilder = new StringBuilder();

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
				public readonly C c;

				public B(C c)
				{
					this.c = c;
				}
			}

			public class C : IInitializable
			{
				public void Initialize()
				{
					logBuilder.Append("Run ");
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				container.Get<B>();
				container.Get<A>();
				Assert.That(logBuilder.ToString(), Is.EqualTo("Run "));
			}
		}

		public class InitializeUsesInstancesCreatedByFactories : InitializeComponentsTest
		{
			private static readonly StringBuilder logBuilder = new StringBuilder();

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
				public readonly C c;

				public B(C c)
				{
					this.c = c;
				}
			}

			public class C : IInitializable
			{
				public void Initialize()
				{
					logBuilder.Append("Run ");
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				container.Get<A>();
				Assert.That(logBuilder.ToString(), Is.EqualTo("Run "));
			}
		}

		public class UnionAllDependencies : InitializeComponentsTest
		{
			public class A
			{
				public readonly IEnumerable<IX> instances;

				public A([TestContract("u")] IEnumerable<IX> instances)
				{
					this.instances = instances;
				}
			}

			public interface IX
			{
			}

			public class B : IX, IInitializable
			{
				public static bool initializeCalled;

				public void Initialize()
				{
					initializeCalled = true;
				}
			}

			public class C : IX, IInitializable
			{
				public static bool initializeCalled;

				public void Initialize()
				{
					initializeCalled = true;
				}
			}

			[Test]
			public void Tets()
			{
				var container = Container(delegate(ContainerConfigurationBuilder b)
				{
					b.Contract("a").Bind<IX, B>();
					b.Contract("b").Bind<IX, C>();
					b.Contract("u").UnionOf("a", "b");
				});

				container.Get<A>();
				Assert.That(B.initializeCalled);
				Assert.That(C.initializeCalled);
			}
		}

		public class InterfaceDependencies : InitializeComponentsTest
		{
			public class A
			{
				public readonly IX x;

				public A(IX x)
				{
					this.x = x;
				}
			}

			public interface IX
			{
			}

			public class X1 : IX
			{
				public readonly Initializable initializable;

				public X1(Initializable initializable)
				{
					this.initializable = initializable;
				}
			}

			public class Initializable : IInitializable
			{
				public static bool initializeCalled;

				public void Initialize()
				{
					initializeCalled = true;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				container.Get<A>();
				Assert.That(Initializable.initializeCalled);
			}
		}

		public class EnumerableDependencies : InitializeComponentsTest
		{
			public class A
			{
				public readonly IEnumerable<IX> dependencies;

				public A(IEnumerable<IX> dependencies)
				{
					this.dependencies = dependencies;
				}
			}

			public interface IX
			{
			}

			public class B : IX, IInitializable
			{
				public static bool initializeCalled;

				public void Initialize()
				{
					initializeCalled = true;
				}
			}

			public class C : IX, IInitializable
			{
				public static bool initializeCalled;

				public void Initialize()
				{
					initializeCalled = true;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				container.Get<A>();
				Assert.That(B.initializeCalled);
				Assert.That(C.initializeCalled);
			}
		}

		public class InitializeComponentsCreatedInFactories : InitializeComponentsTest
		{
			public class A
			{
				public readonly Func<B> createB;

				public A(Func<B> createB)
				{
					this.createB = createB;
				}
			}

			public class B
			{
				public readonly C c;

				public B(C c)
				{
					this.c = c;
				}
			}

			public class C : IInitializable
			{
				public static int initializeCallCount;

				public void Initialize()
				{
					initializeCallCount ++;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var a = container.Get<A>();
				Assert.That(C.initializeCallCount, Is.EqualTo(0));
				var b1 = a.createB();
				Assert.That(C.initializeCallCount, Is.EqualTo(1));
				var b2 = a.createB();
				Assert.That(C.initializeCallCount, Is.EqualTo(1));
				Assert.That(b1, Is.Not.SameAs(b2));
			}
		}

		public class FactoryCallInConstructor_DelayInitializeUntilEntireDependencyTreeIsConstructed : InitializeComponentsTest
		{
			public class A
			{
				public readonly B b;

				public A(B b = null)
				{
					this.b = b;
				}
			}

			public class B
			{
				public readonly C c;
				public readonly D d;

				public B(C c, D d)
				{
					this.c = c;
					this.d = d;
				}
			}

			public class D
			{
			}

			public class C
			{
				public readonly E e1;
				public readonly E e2;

				public C(Func<E> createE)
				{
					e1 = createE();
					e2 = createE();
				}
			}

			public class E : IInitializable
			{
				public static int initializeCallCount;

				public void Initialize()
				{
					initializeCallCount++;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.DontUse<D>());
				Assert.That(container.Get<A>().b, Is.Null);
				Assert.That(E.initializeCallCount, Is.EqualTo(0));
				container.Get<C>();
				Assert.That(E.initializeCallCount, Is.EqualTo(2));
			}
		}

		public class InitializeExceptionMustContainServiceContracts : InitializeComponentsTest
		{
			[TestContract("c1")]
			public class A : IInitializable
			{
				public readonly int parameter;

				public A(int parameter)
				{
					this.parameter = parameter;
				}

				public void Initialize()
				{
					throw new InvalidOperationException("test crash");
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.Contract("c1").BindDependency<A>("parameter", 42));
				var resolvedA = container.Resolve<A>();
				var exception = Assert.Throws<SimpleContainerException>(resolvedA.EnsureInitialized);
				Assert.That(exception.Message, Is.EqualTo("exception initializing A[c1]\r\n\r\nA[c1], initializing ...\r\n\tparameter -> 42"));
				Assert.That(exception.InnerException.Message, Is.EqualTo("test crash"));
			}
		}

		public class DontInitiaizeNotOwnedComponents : InitializeComponentsTest
		{
			public class A : IInitializable
			{
				public static StringBuilder log = new StringBuilder();

				public void Initialize()
				{
					log.Append("Initialize ");
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.Bind<A>(new A(), false));
				container.Get<A>();
				Assert.That(A.log.ToString(), Is.EqualTo(""));
			}
		}

		public class InitializeWithInitializeLogger : InitializeComponentsTest
		{
			private static StringBuilder log;

			protected override void SetUp()
			{
				base.SetUp();
				log = new StringBuilder();
			}

			public class ComponentA : IInitializable
			{
				public readonly ComponentB componentB;

				public ComponentA(ComponentB componentB)
				{
					this.componentB = componentB;
				}

				public void Initialize()
				{
					log.Append(" ComponentA.Initialize\r\n");
				}
			}

			public class ComponentB : IInitializable
			{
				public readonly int parameter;

				public ComponentB(int parameter)
				{
					this.parameter = parameter;
				}

				public void Initialize()
				{
					log.Append(" ComponentB.Initialize\r\n");
				}
			}

			[Test]
			public void Test()
			{
				LogInfo logInfo = delegate(ServiceName name, string message)
				{
					log.Append(name);
					log.Append(" - ");
					log.Append(message);
				};
				Action<ContainerConfigurationBuilder> configure = b => b
					.Contract("my-contract")
					.BindDependency<ComponentB>("parameter", 42);
				using (var container = Factory().WithInfoLogger(logInfo).WithConfigurator(configure).Build())
				{
					container.Get<ComponentA>("my-contract");
					const string componentALog =
						"ComponentA[my-contract] - initialize started ComponentA.Initialize\r\nComponentA[my-contract] - initialize finished";
					const string componentBLog =
						"ComponentB[my-contract] - initialize started ComponentB.Initialize\r\nComponentB[my-contract] - initialize finished";
					Assert.That(log.ToString(), Is.EqualTo(componentBLog + componentALog));
				}
			}
		}

		public class EnsireInitializedMustShowServiceDependencies : InitializeComponentsTest
		{
			public class A
			{
				public readonly B b;

				public A(B b)
				{
					this.b = b;
				}
			}

			public class B: IInitializable
			{
				public void Initialize()
				{
					throw new InvalidOperationException("test-crash");
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();

				var exception = Assert.Throws<SimpleContainerException>(() => container.Get<A>());
				Assert.That(exception.Message, Is.EqualTo("exception initializing B\r\n\r\nA, initializing ...\r\n\tB, initializing ..."));
				Assert.That(exception.InnerException.Message, Is.EqualTo("test-crash"));
			}
		}

		public class InvokeInitializeOnGetOnlyIfItIsTopmost : InitializeComponentsTest
		{
			public class A
			{
				public B b;

				public A(IContainer container)
				{
					b = container.Get<B>();
					Assert.That(B.initializeCalled, Is.False);
				}
			}

			public class B : IInitializable
			{
				public static bool initializeCalled;

				public void Initialize()
				{
					initializeCalled = true;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				container.Get<A>();
				Assert.That(B.initializeCalled, Is.True);
			}
		}
		
		public class InvokeInitializeOnGetAllOnlyIfItIsTopmost : InitializeComponentsTest
		{
			public class A
			{
				public IEnumerable<B> b;

				public A(IContainer container)
				{
					b = container.GetAll<B>();
					Assert.That(B.initializeCalled, Is.False);
				}
			}

			public class B : IInitializable
			{
				public static bool initializeCalled;

				public void Initialize()
				{
					initializeCalled = true;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				container.Get<A>();
				Assert.That(B.initializeCalled, Is.True);
			}
		}

		public class GetAllCallsInitializeOnAllInstances : InitializeComponentsTest
		{
			public interface IService : IInitializable
			{
				bool Initialized { get; set; }
			}

			public class ImplA : IService
			{
				public bool Initialized { get; set; }

				public void Initialize()
				{
					Initialized = true;
				}
			}

			public class ImplB : IService
			{
				public bool Initialized { get; set; }

				public void Initialize()
				{
					Initialized = true;
				}
			}

			[Test]
			public void Test()
			{
				var services = Container().GetAll<IService>().ToArray();
				Assert.That(services.Length, Is.EqualTo(2));
				foreach (var service in services)
					Assert.That(service.Initialized, Is.True);
			}
		}

		public class ResolutionsInInitializeAreProhibited : InitializeComponentsTest
		{
			public class A : IInitializable
			{
				private readonly Lazy<B> lazyB;

				public A(Lazy<B> lazyB)
				{
					this.lazyB = lazyB;
				}

				public B b;

				public void Initialize()
				{
					b = lazyB.Value;
				}
			}

			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var exception = Assert.Throws<SimpleContainerException>(() => container.Get<A>());
				const string expectedTopMessage = @"
exception initializing A

A, initializing ...
	Lazy<B>";
				const string expectedNestedMessage = @"
attempt to resolve [B] is prohibited to prevent possible deadlocks

!B <---------------";
				Assert.That(exception.Message, Is.EqualTo(FormatExpectedMessage(expectedTopMessage)));
				Assert.That(exception.InnerException.Message, Is.EqualTo(FormatExpectedMessage(expectedNestedMessage)));
			}
		}
	}
}