using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Infection;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class RunComponentsTest : SimpleContainerTestBase
	{
		public class Simple : RunComponentsTest
		{
			public class ComponentWrap
			{
				public readonly Component2 component2;

				public ComponentWrap(Component2 component2)
				{
					this.component2 = component2;
				}
			}

			public class Component2 : IComponent
			{
				public readonly IntermediateService intermediateService;

				public Component2(IntermediateService intermediateService)
				{
					this.intermediateService = intermediateService;
					LogBuilder.Append("Component2.ctor ");
				}

				public void Run()
				{
					LogBuilder.Append("Component2.Run ");
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

			public class Component1 : IComponent
			{
				public Component0 component0;

				public Component1(Component0 component0)
				{
					this.component0 = component0;
					LogBuilder.Append("Component1.ctor ");
				}

				public void Run()
				{
					LogBuilder.Append("Component1.Run ");
				}
			}

			public class Component0 : IComponent
			{
				public Component0()
				{
					LogBuilder.Append("Component0.ctor ");
				}

				public void Run()
				{
					LogBuilder.Append("Component0.Run ");
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				container.Get<ComponentWrap>();
				const string constructorsLog = "Component0.ctor Component1.ctor IntermediateService.ctor Component2.ctor ";
				const string runLog = "Component0.Run Component1.Run Component2.Run ";
				Assert.That(LogBuilder.ToString(), Is.EqualTo(constructorsLog + runLog));
			}
		}

		public class RunComponentsFromCache : RunComponentsTest
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

			public class C : IComponent
			{
				public static bool runCalled;

				public void Run()
				{
					runCalled = true;
				}
			}

			public class D
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.DontUse<D>());
				container.Resolve<A>().Run();
				Assert.That(C.runCalled);
			}
		}

		public class DoNotRunNotUsedComponents : RunComponentsTest
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

			public class B : IComponent
			{
				public void Run()
				{
					logBuilder.Append("Run ");
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

		public class RunUsesInstancesReachableFromAllResolutionContexts : RunComponentsTest
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

			public class C : IComponent
			{
				public void Run()
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

		public class RunUsesInstancesCreatedByFactories : RunComponentsTest
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

			public class C : IComponent
			{
				public void Run()
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

		public class UnionAllDependencies : RunComponentsTest
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

			public class B : IX, IComponent
			{
				public static bool runCalled;

				public void Run()
				{
					runCalled = true;
				}
			}

			public class C : IX, IComponent
			{
				public static bool runCalled;

				public void Run()
				{
					runCalled = true;
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
				Assert.That(B.runCalled);
				Assert.That(C.runCalled);
			}
		}

		public class InterfaceDependencies : RunComponentsTest
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
				public readonly Component component;

				public X1(Component component)
				{
					this.component = component;
				}
			}

			public class Component : IComponent
			{
				public static bool runCalled;

				public void Run()
				{
					runCalled = true;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				container.Get<A>();
				Assert.That(Component.runCalled);
			}
		}

		public class EnumerableDependencies : RunComponentsTest
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

			public class B : IX, IComponent
			{
				public static bool runCalled;

				public void Run()
				{
					runCalled = true;
				}
			}

			public class C : IX, IComponent
			{
				public static bool runCalled;

				public void Run()
				{
					runCalled = true;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				container.Get<A>();
				Assert.That(B.runCalled);
				Assert.That(C.runCalled);
			}
		}

		public class RunComponentsCreatedInFactories : RunComponentsTest
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

			public class C : IComponent
			{
				public static int runCallCount;

				public void Run()
				{
					runCallCount ++;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var a = container.Get<A>();
				Assert.That(C.runCallCount, Is.EqualTo(0));
				var b1 = a.createB();
				Assert.That(C.runCallCount, Is.EqualTo(1));
				var b2 = a.createB();
				Assert.That(C.runCallCount, Is.EqualTo(1));
				Assert.That(b1, Is.Not.SameAs(b2));
			}
		}

		public class FactoryCallInConstructor_DelayRunUntilEntireDependencyTreeIsConstructed : RunComponentsTest
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

			public class E : IComponent
			{
				public static int runCallCount;

				public void Run()
				{
					runCallCount++;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.DontUse<D>());
				Assert.That(container.Get<A>().b, Is.Null);
				Assert.That(E.runCallCount, Is.EqualTo(0));
				container.Get<C>();
				Assert.That(E.runCallCount, Is.EqualTo(2));
			}
		}

		public class RunExceptionMustContainServiceContracts : RunComponentsTest
		{
			[TestContract("c1")]
			public class A : IComponent
			{
				public readonly int parameter;

				public A(int parameter)
				{
					this.parameter = parameter;
				}

				public void Run()
				{
					throw new InvalidOperationException("test crash");
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.Contract("c1").BindDependency<A>("parameter", 42));
				var resolvedA = container.Resolve<A>();
				var exception = Assert.Throws<SimpleContainerException>(() => resolvedA.Run());
				Assert.That(exception.Message, Is.EqualTo("exception running A[c1]"));
				Assert.That(exception.InnerException.Message, Is.EqualTo("test crash"));
			}
		}

		public class RunWithRunLogger : RunComponentsTest
		{
			private static StringBuilder log;

			protected override void SetUp()
			{
				base.SetUp();
				log = new StringBuilder();
			}

			public class ComponentA : IComponent
			{
				public readonly ComponentB componentB;

				public ComponentA(ComponentB componentB)
				{
					this.componentB = componentB;
				}

				public void Run()
				{
					log.Append(" ComponentA.Run\r\n");
				}
			}

			public class ComponentB : IComponent
			{
				public readonly int parameter;

				public ComponentB(int parameter)
				{
					this.parameter = parameter;
				}

				public void Run()
				{
					log.Append(" ComponentB.Run\r\n");
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
						"ComponentA[my-contract] - run started ComponentA.Run\r\nComponentA[my-contract] - run finished";
					const string componentBLog =
						"ComponentB[my-contract] - run started ComponentB.Run\r\nComponentB[my-contract] - run finished";
					Assert.That(log.ToString(), Is.EqualTo(componentBLog + componentALog));
				}
			}
		}
	}
}