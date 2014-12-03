using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Implementation;
using SimpleContainer.Infection;

namespace SimpleContainer.Tests
{
	public abstract class ContainerConfigurationTest : SimpleContainerTestBase
	{
		public class SimpleConfigurators : ContainerConfigurationTest
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

		public class ConfiguratorsWithSettings : ContainerConfigurationTest
		{
			public class Service
			{
				public readonly string parameter;

				public Service(string parameter)
				{
					this.parameter = parameter;
				}
			}

			public class MySubsystemSettings
			{
				public string MyParameter { get; set; }
			}

			public class ServiceConfigurator : IServiceConfigurator<MySubsystemSettings, Service>
			{
				public void Configure(MySubsystemSettings settings, ServiceConfigurationBuilder<Service> builder)
				{
					builder.Dependencies(new
					{
						parameter = settings.MyParameter
					});
				}
			}

			public class OtherConfigurator : IServiceConfigurator<MySubsystemSettings, Service>
			{
				public void Configure(MySubsystemSettings settings, ServiceConfigurationBuilder<Service> builder)
				{
				}
			}

			[Test]
			public void Test()
			{
				Func<Type, object> loadSettings = t => new MySubsystemSettings {MyParameter = "abc"};
				using (var staticContainer = CreateStaticContainer(x => x.SetSettingsLoader(loadSettings)))
				using (var localContainer = LocalContainer(staticContainer, null))
				{
					var instance = localContainer.Get<Service>();
					Assert.That(instance.parameter, Is.EqualTo("abc"));
				}
			}

			[Test]
			public void LoadSettingsOnce()
			{
				var log = new StringBuilder();
				Func<Type, object> loadSettings = t =>
				{
					log.AppendFormat("load {0} ", t.Name);
					return new MySubsystemSettings {MyParameter = "abc"};
				};
				using (var staticContainer = CreateStaticContainer(x => x.SetSettingsLoader(loadSettings)))
				using (LocalContainer(staticContainer, null))
				{
				}
				Assert.That(log.ToString(), Is.EqualTo("load MySubsystemSettings "));
			}
		}

		public class SettingsLoaderErrors : ContainerConfigurationTest
		{
			public class Service
			{
				public readonly string parameter;

				public Service(string parameter)
				{
					this.parameter = parameter;
				}
			}

			public class MySubsystemSettings
			{
				public string MyParameter { get; set; }
			}

			public class OtherSubsystemSettings
			{
			}

			public class ServiceConfigurator : IServiceConfigurator<MySubsystemSettings, Service>
			{
				public void Configure(MySubsystemSettings settings, ServiceConfigurationBuilder<Service> builder)
				{
					builder.Dependencies(new
					{
						parameter = settings.MyParameter
					});
				}
			}

			[Test]
			public void SettingsLoaderIsNotConfigured()
			{
				using (var staticContainer = CreateStaticContainer())
				{
					var error = Assert.Throws<SimpleContainerException>(() => LocalContainer(staticContainer, null));
					const string expectedMessage =
						"configurator [ServiceConfigurator] requires settings, but settings loader is not configured;" +
						"configure it using ContainerFactory.SetSettingsLoader";
					Assert.That(error.Message, Is.EqualTo(expectedMessage));
				}
			}

			[Test]
			public void SettingsLoaderRetursNull()
			{
				Func<Type, object> loadSettings = t => null;
				using (var staticContainer = CreateStaticContainer(x => x.SetSettingsLoader(loadSettings)))
				{
					var error = Assert.Throws<SimpleContainerException>(() => LocalContainer(staticContainer, null));
					const string expectedMessage = "configurator [ServiceConfigurator] requires settings, " +
					                               "but settings loader returned null";
					Assert.That(error.Message, Is.EqualTo(expectedMessage));
				}
			}

			[Test]
			public void SettingsLoaderReturnsObjectOfInvalidType()
			{
				Func<Type, object> loadSettings = t => new OtherSubsystemSettings();
				using (var staticContainer = CreateStaticContainer(x => x.SetSettingsLoader(loadSettings)))
				{
					var error = Assert.Throws<SimpleContainerException>(() => LocalContainer(staticContainer, null));
					const string expectedMessage = "configurator [ServiceConfigurator] requires settings [MySubsystemSettings], " +
					                               "but settings loader returned [OtherSubsystemSettings]";
					Assert.That(error.Message, Is.EqualTo(expectedMessage));
				}
			}
		}

		public class ServiceCanHaveManyConfigurators : ContainerConfigurationTest
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

		public class CanUseConfiguratorNotBoundToService : ContainerConfigurationTest
		{
			public interface IInterface
			{
				int Value { get; }
			}

			public class Impl : IInterface
			{
				public Impl(int value)
				{
					Value = value;
				}

				public int Value { get; private set; }
			}

			public class Wrap
			{
				public readonly IEnumerable<IInterface> instances;

				public Wrap([RequireContract("composite-contract")] IEnumerable<IInterface> instances)
				{
					this.instances = instances;
				}
			}

			public class CompositeContractConfigurator : IContainerConfigurator
			{
				public void Configure(ContainerConfigurationBuilder builder)
				{
					builder.Contract("composite-contract").UnionOf("c1", "c2");
				}
			}

			public class ImplConfigurator : IServiceConfigurator<Impl>
			{
				public void Configure(ServiceConfigurationBuilder<Impl> builder)
				{
					builder.Contract("c1").Dependencies(new {value = 1});
					builder.Contract("c2").Dependencies(new {value = 2});
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var wrap = container.Get<Wrap>();
				Assert.That(wrap.instances.Select(x => x.Value), Is.EquivalentTo(new[] {1, 2}));
			}
		}

		public class ContainerConfiguratorWithSettings : ContainerConfigurationTest
		{
			public class MySettings
			{
				public int value;
			}

			public class SomeService
			{
				public SomeService(int value)
				{
					Value = value;
				}

				public int Value { get; private set; }
			}

			public class MyConfigurator : IContainerConfigurator<MySettings>
			{
				public void Configure(MySettings settings, ContainerConfigurationBuilder builder)
				{
					builder.BindDependency<SomeService>("value", settings.value);
				}
			}

			[Test]
			public void Test()
			{
				Func<Type, object> loadSettings = t => new MySettings {value = 87};
				using (var staticContainer = CreateStaticContainer(x => x.SetSettingsLoader(loadSettings)))
					Assert.That(LocalContainer(staticContainer, null).Get<SomeService>().Value, Is.EqualTo(87));
			}
		}

		public class CanAppendContracts : ContractsTest
		{
			public class A
			{
				public readonly int p1;
				public readonly int p2;

				public A(int p1, int p2)
				{
					this.p1 = p1;
					this.p2 = p2;
				}
			}

			public class AConfigurator1 : IServiceConfigurator<A>
			{
				public void Configure(ServiceConfigurationBuilder<A> builder)
				{
					builder.Contract("a").Dependencies(new {p1 = 1});
				}
			}

			public class AConfigurator2 : IServiceConfigurator<A>
			{
				public void Configure(ServiceConfigurationBuilder<A> builder)
				{
					builder.Contract("a").Dependencies(new {p2 = 2});
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var a = container.Create<A>("a");
				Assert.That(a.p1, Is.EqualTo(1));
				Assert.That(a.p2, Is.EqualTo(2));
			}
		}

		public class CanBindDependenciesViaAnonymousType : ContainerConfigurationTest
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
	}
}