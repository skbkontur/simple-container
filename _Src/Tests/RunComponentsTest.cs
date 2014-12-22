using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Hosting;
using SimpleContainer.Infection;
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
				container.Run<ComponentWrap>(null);
				const string constructorsLog = "Component0.ctor Component1.ctor IntermediateService.ctor Component2.ctor ";
				const string runLog = "Component0.Run Component1.Run Component2.Run ";
				Assert.That(LogBuilder.ToString(), Is.EqualTo(constructorsLog + runLog));
			}
		}

		public class CorrectOrderingWhenContractsUsed : RunComponentsTest
		{
			public class A
			{
				public readonly B b;
				public readonly C c;

				public A([RequireContract("x")] B b, [RequireContract("x")] C c)
				{
					this.b = b;
					this.c = c;
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

			public class C
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(x => x.Contract("x"));
				container.Get<A>();
				var instanceCache = container.GetClosure(typeof (A), null);
				Assert.That(instanceCache.Select(x => x.Instance.GetType()).ToArray(),
					Is.EqualTo(new[] {typeof (C), typeof (B), typeof (A)}));
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
				container.Run<A>(null);
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
				var a = container.Run<A>(null);
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
				container.Run<A>(null);
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
				container.Run<A>(null);
				Assert.That(logBuilder.ToString(), Is.EqualTo("Run "));
			}
		}

		public class UnionAllDependencies : RunComponentsTest
		{
			public class A
			{
				public readonly IEnumerable<IX> instances;

				public A([RequireContract("u")] IEnumerable<IX> instances)
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

				container.Run<A>();
				Assert.That(B.runCalled);
				Assert.That(C.runCalled);
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
				container.Run<A>();
				Assert.That(B.runCalled);
				Assert.That(C.runCalled);
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

			public class SimpleComponentLogger : IComponentLogger
			{
				public IDisposable OnRunComponent(ServiceInstance<IComponent> component)
				{
					log.Append(component.FormatName() + ".start\r\n");
					return new ActionDisposable(() => log.Append(component.FormatName() + ".finish\r\n"));
				}

				public void TRASH_DumpConstructionLog(string constructionLog)
				{
				}
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
					log.Append("ComponentA.Run\r\n");
				}
			}

			public class ComponentB : IComponent
			{
				public void Run()
				{
					log.Append("ComponentB.Run\r\n");
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				container.Run<ComponentA>(null);
				const string componentALog = "ComponentA.start\r\nComponentA.Run\r\nComponentA.finish\r\n";
				const string componentBLog = "ComponentB.start\r\nComponentB.Run\r\nComponentB.finish\r\n";
				Assert.That(log.ToString(), Is.EqualTo(componentBLog + componentALog));
			}
		}
	}
}