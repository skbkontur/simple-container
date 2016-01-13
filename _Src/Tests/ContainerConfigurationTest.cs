using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

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
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<IInterface> builder)
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

			public class ServiceConfigurator : IServiceConfigurator<Service>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<Service> builder)
				{
					builder.Dependencies(new
					{
						parameter = context.Settings<MySubsystemSettings>().MyParameter
					});
				}
			}

			public class OtherConfigurator : IServiceConfigurator<Service>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<Service> builder)
				{
				}
			}

			[Test]
			public void Test()
			{
				Func<Type, object> loadSettings = t => new MySubsystemSettings {MyParameter = "abc"};
				using (var staticContainer = Factory().WithSettingsLoader(loadSettings).Build())
				{
					var instance = staticContainer.Get<Service>();
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
				using (var container = Factory().WithSettingsLoader(loadSettings).Build())
					container.Get<Service>();
				Assert.That(log.ToString(), Is.EqualTo("load MySubsystemSettings "));
			}
		}

		public class ServiceConfiguratorsAreLazy : ContainerConfigurationTest
		{
			public class A
			{
				public readonly int parameter;

				public A(int parameter)
				{
					this.parameter = parameter;
				}
			}

			public class AConfigurator : IServiceConfigurator<A>
			{
				public static int callCount;

				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<A> builder)
				{
					callCount++;
					builder.Dependencies(new {parameter = 72});
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(AConfigurator.callCount, Is.EqualTo(0));
				Assert.That(container.Get<A>().parameter, Is.EqualTo(72));
				Assert.That(AConfigurator.callCount, Is.EqualTo(1));
				Assert.That(container.Get<Func<A>>().Invoke().parameter, Is.EqualTo(72));
				Assert.That(AConfigurator.callCount, Is.EqualTo(1));
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

			public class ServiceConfigurator : IServiceConfigurator<Service>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<Service> builder)
				{
					builder.Dependencies(new
					{
						parameter = context.Settings<MySubsystemSettings>().MyParameter
					});
				}
			}

			[Test]
			public void SettingsLoaderIsNotConfigured()
			{
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<Service>());
				Assert.That(error.InnerException.InnerException.Message,
					Is.EqualTo("settings loader is not configured, use ContainerFactory.WithSettingsLoader"));
			}

			[Test]
			public void SettingsLoaderRetursNull()
			{
				Func<Type, object> loadSettings = t => null;
				using (var container = Factory().WithSettingsLoader(loadSettings).Build())
				{
					var error = Assert.Throws<SimpleContainerException>(() => container.Get<Service>());
					Assert.That(error.InnerException.InnerException.Message,
						Is.EqualTo("settings loader returned null for type [MySubsystemSettings]"));
				}
			}

			[Test]
			public void SettingsLoaderReturnsObjectOfInvalidType()
			{
				Func<Type, object> loadSettings = t => new OtherSubsystemSettings();
				using (var container = Factory().WithSettingsLoader(loadSettings).Build())
				{
					var error = Assert.Throws<SimpleContainerException>(() => container.Get<Service>());
					Assert.That(error.InnerException.InnerException.Message,
						Is.EqualTo("invalid settings type, required [MySubsystemSettings], actual [OtherSubsystemSettings]"));
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
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<Service> builder)
				{
					builder.Dependencies(new
					{
						argument1 = 1
					});
				}
			}

			public class ServiceConfigurator2 : IServiceConfigurator<Service>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<Service> builder)
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

				public Wrap([TestContract("composite-contract")] IEnumerable<IInterface> instances)
				{
					this.instances = instances;
				}
			}

			public class CompositeContractConfigurator : IContainerConfigurator
			{
				public void Configure(ConfigurationContext context, ContainerConfigurationBuilder builder)
				{
					builder.Contract("composite-contract").UnionOf("c1", "c2");
				}
			}

			public class ImplConfigurator : IServiceConfigurator<Impl>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<Impl> builder)
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

			public class MyConfigurator : IContainerConfigurator
			{
				public void Configure(ConfigurationContext context, ContainerConfigurationBuilder builder)
				{
					builder.BindDependency<SomeService>("value", context.Settings<MySettings>().value);
				}
			}

			[Test]
			public void Test()
			{
				Func<Type, object> loadSettings = t => new MySettings {value = 87};
				using (var container = Factory().WithSettingsLoader(loadSettings).Build())
					Assert.That(container.Get<SomeService>().Value, Is.EqualTo(87));
			}
		}

		public class ContainerConfiguratorWithSettingsWithKey : ContainerConfigurationTest
		{
			public class MySettings
			{
				public string value;
			}

			public class SomeService
			{
				public SomeService(string value)
				{
					Value = value;
				}

				public string Value { get; private set; }
			}

			public class MyConfigurator : IContainerConfigurator
			{
				public void Configure(ConfigurationContext context, ContainerConfigurationBuilder builder)
				{
					builder.BindDependency<SomeService>("value", context.Settings<MySettings>("my_context_key").value);
				}
			}

			[Test]
			public void Test()
			{
				Func<Type, string, object> loadSettings = (t, key) => new MySettings {value = key};
				using (var container = Factory().WithSettingsLoader(loadSettings).Build())
					Assert.That(container.Get<SomeService>().Value, Is.EqualTo("my_context_key"));
			}
		}

		public class CanAppendContracts : ContainerConfigurationTest
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
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<A> builder)
				{
					builder.Contract("a").Dependencies(new {p1 = 1});
				}
			}

			public class AConfigurator2 : IServiceConfigurator<A>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<A> builder)
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

		public class GenericConfigurators : ContainerConfigurationTest
		{
			public class GenericReader<TItem>
				where TItem : IItem
			{
				public readonly int parameter;

				public GenericReader(int parameter)
				{
					this.parameter = parameter;
				}
			}

			public interface IItem
			{
			}

			public class SimpleItem : IItem
			{
			}

			public class ComplexItem : IItem
			{
			}

			public class GenericReaderConfigurator<TItem> : IServiceConfigurator<GenericReader<TItem>> where TItem : IItem
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<GenericReader<TItem>> builder)
				{
					builder.Dependencies(new {parameter = 12});
				}
			}

			[Test]
			public void Test()
			{
				var contaier = Container();
				var simpleReader = contaier.Get<GenericReader<SimpleItem>>();
				Assert.That(simpleReader.parameter, Is.EqualTo(12));
				var complexReader = contaier.Get<GenericReader<ComplexItem>>();
				Assert.That(complexReader.parameter, Is.EqualTo(12));
			}
		}

		public class ImplicitlyCastInt32ToInt64 : ContainerConfigurationTest
		{
			public class Service
			{
				public readonly long parameter;

				public Service(long parameter)
				{
					this.parameter = parameter;
				}
			}

			public class ServiceConfigurator : IServiceConfigurator<Service>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<Service> builder)
				{
					builder.Dependencies(new {parameter = 42});
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<Service>().parameter, Is.EqualTo(42));
			}
		}

		public class ImplicitlyCastInt32ToNullableInt64 : ContainerConfigurationTest
		{
			public class Service
			{
				public readonly long? parameter;

				public Service(long? parameter)
				{
					this.parameter = parameter;
				}
			}

			public class ServiceConfigurator : IServiceConfigurator<Service>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<Service> builder)
				{
					builder.Dependencies(new {parameter = 42});
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<Service>().parameter, Is.EqualTo(42));
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
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<TestService> builder)
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

		public class CanUseParametersSourceFromContext : ContainerConfigurationTest
		{
			public class A
			{
				public readonly int parameter;

				public A(int parameter)
				{
					this.parameter = parameter;
				}
			}

			public class AConfigurator : IServiceConfigurator<A>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<A> builder)
				{
					builder.Dependencies(context.Parameters);
				}
			}

			[Test]
			public void Test()
			{
				var parameters = new TestingParametersSource(new Dictionary<string, object> {{"parameter", 42}});
				using (var container = Factory().WithParameters(parameters).Build())
					Assert.That(container.Get<A>().parameter, Is.EqualTo(42));
			}
		}

		public class ConfiguratorDoNotNeedToCheckParametersSourceForNull : ContainerConfigurationTest
		{
			public class A
			{
				public readonly bool parametersIsNull;

				public A(bool parametersIsNull)
				{
					this.parametersIsNull = parametersIsNull;
				}
			}

			public class AConfigurator : IServiceConfigurator<A>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<A> builder)
				{
					builder.Dependencies(new {parametersIsNull = context.Parameters == null});
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<A>().parametersIsNull, Is.False);
			}
		}

		public class InvalidDependencyValueType : ContainerConfigurationTest
		{
			public class A
			{
				public readonly IEnumerable<string> dependency;

				public A(IEnumerable<string> dependency)
				{
					this.dependency = dependency;
				}
			}

			public class AConfigurator : IServiceConfigurator<A>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<A> builder)
				{
					builder.Dependencies(new {dependency = "invalidValue"});
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var exception = Assert.Throws<SimpleContainerException>(() => container.Get<A>());
				const string expectedMessage =
					"can't cast value [invalidValue] from [string] to [IEnumerable<string>] for dependency [dependency]\r\n\r\n!A\r\n\t!dependency <---------------";
				Assert.That(exception.Message, Is.EqualTo(expectedMessage));
			}
		}

		public class CanBindArray : ContainerConfigurationTest
		{
			public class A
			{
				public readonly IEnumerable<string> dependency;

				public A(IEnumerable<string> dependency)
				{
					this.dependency = dependency;
				}
			}

			public class AConfigurator : IServiceConfigurator<A>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<A> builder)
				{
					builder.Dependencies(new {dependency = new[] {"a", "b"}});
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var instance = container.Get<A>();
				Assert.That(instance.dependency, Is.EqualTo(new[] {"a", "b"}));
			}
		}

		public class Priorities : ContainerConfigurationTest
		{
			public class A
			{
				public readonly int parameter;

				public A(int parameter)
				{
					this.parameter = parameter;
				}
			}

			public interface IHighPriorityServiceConfigurator<T> : IServiceConfigurator<T>
			{
			}

			public class AConfigurator1 : IHighPriorityServiceConfigurator<A>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<A> builder)
				{
					builder.Dependencies(new {parameter = 42});
				}
			}

			public class AConfigurator2 : IServiceConfigurator<A>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<A> builder)
				{
					builder.Dependencies(new {parameter = 43});
				}
			}

			[Test]
			public void Test()
			{
				var priorities = new[] {typeof (IServiceConfigurator<>), typeof (IHighPriorityServiceConfigurator<>)};
				using (var container = Factory().WithPriorities(priorities).Build())
					Assert.That(container.Get<A>().parameter, Is.EqualTo(42));
			}
		}

		public class Profiles : ContainerConfigurationTest
		{
			public class InMemoryProfile : IProfile
			{
			}

			public interface IDatabase
			{
			}

			public class InMemoryDatabase : IDatabase
			{
			}

			public class Database : IDatabase
			{
			}

			public class DatabaseConfigurator : IServiceConfigurator<IDatabase>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<IDatabase> builder)
				{
					if (context.ProfileIs<InMemoryProfile>())
						builder.Bind<InMemoryDatabase>();
					else
						builder.Bind<Database>();
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<IDatabase>(), Is.InstanceOf<Database>());

				using (var inMemoryContainer = Factory().WithProfile(typeof (InMemoryProfile)).Build())
					Assert.That(inMemoryContainer.Get<IDatabase>(), Is.InstanceOf<InMemoryDatabase>());
			}
		}

		public class CanGiveConfiguratorTypeLowerThanDefaultPriority : SimpleContainerTestBase
		{
			public interface IService
			{
			}

			public class ImplOne : IService
			{
			}

			private class ImplTwo : IService
			{
			}

			public interface ILowPriorityConfigurator<T> : IServiceConfigurator<T>
			{
			}

			public class LowPriorityConfigurator : ILowPriorityConfigurator<IService>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<IService> builder)
				{
					builder.Bind<ImplOne>();
				}
			}

			public class DefaultConfigurator : IServiceConfigurator<IService>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<IService> builder)
				{
					builder.Bind<ImplTwo>();
				}
			}

			[Test]
			public void Test()
			{
				var container = Factory()
					.WithPriorities(typeof (ILowPriorityConfigurator<>), typeof (IServiceConfigurator<>))
					.Build();
				Assert.That(container.GetImplementationsOf<IService>(), Is.EqualTo(new[] {typeof (ImplTwo)}));
			}
		}

		public class ServiceConfiguratorForGenericDefinition : SimpleContainerTestBase
		{
			public class G<T>
				where T : IConstraint
			{
				public readonly int parameter;

				public G(int parameter)
				{
					this.parameter = parameter;
				}
			}

			public interface IConstraint
			{
			}

			public class A : IConstraint
			{
			}

			public class B : IConstraint
			{
			}

			public class GConfigurator : IServiceConfigurator<DefinitionOf<G<IConstraint>>>
			{
				public void Configure(ConfigurationContext context,
					ServiceConfigurationBuilder<DefinitionOf<G<IConstraint>>> builder)
				{
					builder.Dependencies(new {parameter = 42});
				}
			}

			public class GBConfigurator : IServiceConfigurator<G<B>>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<G<B>> builder)
				{
					builder.Dependencies(new {parameter = 43});
				}
			}


			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<G<A>>().parameter, Is.EqualTo(42));
				Assert.That(container.Get<G<B>>().parameter, Is.EqualTo(43));
			}
		}

		public class ConfiguratorTypeNotIncludedInPriorities_DoNotApply : SimpleContainerTestBase
		{
			public interface IExcludedConfigurator<T> : IServiceConfigurator<T>
			{
			}

			public class Service
			{
			}

			public class ExcludedConfiguratorImplementation : IExcludedConfigurator<Service>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<Service> builder)
				{
					builder.DontUse();
				}
			}

			[Test]
			public void Test()
			{
				var container = Factory()
					.WithPriorities(typeof(IServiceConfigurator<>))
					.Build();

				Assert.That(container.GetImplementationsOf<Service>(), Is.Not.Empty);
			}
		}
	}
}