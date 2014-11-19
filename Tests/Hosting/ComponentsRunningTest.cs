using NUnit.Framework;
using SimpleContainer.Hosting;

namespace SimpleContainer.Tests.Hosting
{
	public abstract class ComponentsRunningTest : SimpleContainerTestBase
	{
		public class ComponentDependencies : ContainerDependenciesTest
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
				container.Run();
				Assert.That(LogBuilder.ToString(), Is.EqualTo(""));
				container.Get<ComponentWrap>();
				Assert.That(LogBuilder.ToString(), Is.EqualTo("Component0.ctor Component1.ctor IntermediateService.ctor Component2.ctor "));
				LogBuilder.Clear();
				container.Run();
				Assert.That(LogBuilder.ToString(), Is.EqualTo("Component0.Run Component1.Run Component2.Run "));
			}
		}
	}
}