using System;
using System.Text;
using NUnit.Framework;
using SimpleContainer.Hosting;
using SimpleContainer.Infection;

namespace SimpleContainer.Tests.Hosting
{
	public abstract class HostingTest : SimpleContainerTestBase
	{
		protected override void SetUp()
		{
			base.SetUp();
			LogBuilder = new StringBuilder();
		}

		public static StringBuilder LogBuilder { get; private set; }
		
		public class SimpleConfigurators : HostingTest
		{
			public interface IInterface
			{
			}

			public class Impl1 : IInterface
			{
			}

			public class Impl2 : IInterface
			{
			}

			public class InterfaceConfigurator : IServiceConfigurator<IInterface>
			{
				public void Configure(ServiceConfigurationBuilder<IInterface> builder)
				{
					builder.Bind<Impl2>();
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<IInterface>(), Is.InstanceOf<Impl2>());
			}
		}

		public class ServiceCanHaveManyConfigurators : HostingTest
		{
			public class Service
			{
				public readonly int argument1;
				public readonly int argument2;

				public Service(int argument1, int argument2)
				{
					this.argument1 = argument1;
					this.argument2 = argument2;
				}
			}

			public class ServiceConfigurator1 : IServiceConfigurator<Service>
			{
				public void Configure(ServiceConfigurationBuilder<Service> builder)
				{
					builder.Dependencies(new
					{
						argument1 = 1
					});
				}
			}
			
			public class ServiceConfigurator2 : IServiceConfigurator<Service>
			{
				public void Configure(ServiceConfigurationBuilder<Service> builder)
				{
					builder.Dependencies(new
					{
						argument2 = 2
					});
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var instance = container.Get<Service>();
				Assert.That(instance.argument1, Is.EqualTo(1));
				Assert.That(instance.argument2, Is.EqualTo(2));
			}
		}

		public class CanBindDependenciesViaAnonymousType : HostingTest
		{
			public class TestService
			{
				public readonly string stringVal;
				public readonly int intVal;

				public TestService(string stringVal, int intVal)
				{
					this.stringVal = stringVal;
					this.intVal = intVal;
				}
			}

			public class InterfaceConfigurator : IServiceConfigurator<TestService>
			{
				public void Configure(ServiceConfigurationBuilder<TestService> builder)
				{
					builder.Dependencies(new
					{
						stringVal = "testString",
						intVal = 42
					});
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var instance = container.Get<TestService>();
				Assert.That(instance.stringVal, Is.EqualTo("testString"));
				Assert.That(instance.intVal, Is.EqualTo(42));
			}
		}

		public class StopAllComponentsEvenIfSomeOfThemCrashed : HostingTest
		{
			public class Component1 : IComponent
			{
				public readonly Component2 component2;

				public Component1(Component2 component2)
				{
					this.component2 = component2;
				}

				public void Run(ComponentHostingOptions options)
				{
					options.OnStop = delegate
					{
						LogBuilder.AppendLine("Component1.OnStop ");
						throw new InvalidOperationException("test component1 crash");
					};
				}
			}

			public class Component2: IComponent
			{
				public void Run(ComponentHostingOptions options)
				{
					options.OnStop = delegate
					{
						LogBuilder.AppendLine("Component2.OnStop ");
						throw new InvalidOperationException("test component2 crash");
					};
				}
			}

			[Test]
			public void Test()
			{
				Component1 component1;
				var disposable = StartHosting(null, out component1);
				var error = Assert.Throws<AggregateException>(disposable.Dispose);
				Assert.That(error.Message, Is.EqualTo("error stopping components"));
				Assert.That(error.InnerExceptions[0].Message, Is.EqualTo("error stopping component [Component1]"));
				Assert.That(error.InnerExceptions[0].InnerException.Message, Is.EqualTo("test component1 crash"));
				Assert.That(error.InnerExceptions[1].Message, Is.EqualTo("error stopping component [Component2]"));
				Assert.That(error.InnerExceptions[1].InnerException.Message, Is.EqualTo("test component2 crash"));
			}
		}

		public class CloseOverContractWhenHostingUsingServiceHost : HostingTest
		{
			public class Hoster
			{
				public readonly IServiceHost serviceHost;

				public Hoster([RequireContract("impl2-contract")] IServiceHost serviceHost)
				{
					this.serviceHost = serviceHost;
				}
			}

			public interface IMyInterface
			{
			}

			public class Impl1 : IMyInterface
			{
			}
			
			public class Impl2 : IMyInterface
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.Contract("impl2-contract").Bind<IMyInterface, Impl2>());
				var hoster = container.Get<Hoster>();
				IMyInterface myInterface;
				using (hoster.serviceHost.StartHosting(out myInterface))
					Assert.That(myInterface.GetType(), Is.EqualTo(typeof (Impl2)));
			}
		}

		public class CanInjectServiceHost : HostingTest
		{
			public class Hoster
			{
				public readonly IServiceHost serviceHost;

				public Hoster(IServiceHost serviceHost)
				{
					this.serviceHost = serviceHost;
				}
			}

			public class ComponentWrap
			{
				public readonly Component component;

				public ComponentWrap(Component component)
				{
					this.component = component;
				}
			}

			public class Component: IComponent
			{
				public void Run(ComponentHostingOptions options)
				{
					LogBuilder.Append("Component.Run ");
					options.OnStop = () => LogBuilder.Append("Component.OnStop ");
				}
			}

			[Test]
			public void Test()
			{
				Hoster hoster;
				using (StartHosting(null, out hoster))
				{
					Assert.That(LogBuilder.ToString(), Is.EqualTo(""));
					ComponentWrap wrap;
					using (hoster.serviceHost.StartHosting(out wrap))
						Assert.That(LogBuilder.ToString(), Is.EqualTo("Component.Run "));
					Assert.That(LogBuilder.ToString(), Is.EqualTo("Component.Run Component.OnStop "));
					LogBuilder.Clear();
					using (hoster.serviceHost.StartHosting(out wrap))
						Assert.That(LogBuilder.ToString(), Is.EqualTo("Component.Run "));
					Assert.That(LogBuilder.ToString(), Is.EqualTo("Component.Run Component.OnStop "));
					LogBuilder.Clear();
				}
				Assert.That(LogBuilder.ToString(), Is.EqualTo(""));
			}
		}

		public class ComponentDependencies : HostingTest
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

				public void Run(ComponentHostingOptions options)
				{
					LogBuilder.Append("Component2.Run ");
					options.OnStop = () => LogBuilder.Append("Component2.OnStop ");
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

			public class Component1: IComponent
			{
				public Component0 component0;

				public Component1(Component0 component0)
				{
					this.component0 = component0;
					LogBuilder.Append("Component1.ctor ");
				}

				public void Run(ComponentHostingOptions options)
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

				public void Run(ComponentHostingOptions options)
				{
					LogBuilder.Append("Component0.Run ");
					options.OnStop = () => LogBuilder.Append("Component0.OnStop ");
				}
			}

			[Test]
			public void Test()
			{
				ComponentWrap wrap;
				using (StartHosting(null, out wrap))
				{
					const string expectedLog = "Component0.ctor Component1.ctor IntermediateService.ctor Component2.ctor " +
					                           "Component0.Run Component1.Run Component2.Run ";
					Assert.That(LogBuilder.ToString(), Is.EqualTo(expectedLog));
					LogBuilder.Clear();
				}
				Assert.That(LogBuilder.ToString(), Is.EqualTo("Component2.OnStop Component0.OnStop "));
			}
		}
	}
}