using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
				using (var staticContainer = CreateStaticContainer(x => x.WithSettingsLoader(loadSettings)))
				using (var localContainer = LocalContainer(staticContainer))
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
				using (var staticContainer = CreateStaticContainer(x => x.WithSettingsLoader(loadSettings)))
				using (LocalContainer(staticContainer))
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
				using (var staticContainer = CreateStaticContainer())
				{
					var error = Assert.Throws<SimpleContainerException>(() => LocalContainer(staticContainer));
					Assert.That(error.Message, Is.EqualTo("settings loader is not configured, use ContainerFactory.WithSettingsLoader"));
				}
			}

			[Test]
			public void SettingsLoaderRetursNull()
			{
				Func<Type, object> loadSettings = t => null;
				using (var staticContainer = CreateStaticContainer(x => x.WithSettingsLoader(loadSettings)))
				{
					var error = Assert.Throws<SimpleContainerException>(() => LocalContainer(staticContainer));
					Assert.That(error.Message, Is.EqualTo("settings loader returned null for type [MySubsystemSettings]"));
				}
			}

			[Test]
			public void SettingsLoaderReturnsObjectOfInvalidType()
			{
				Func<Type, object> loadSettings = t => new OtherSubsystemSettings();
				using (var staticContainer = CreateStaticContainer(x => x.WithSettingsLoader(loadSettings)))
				{
					var error = Assert.Throws<SimpleContainerException>(() => LocalContainer(staticContainer));
					Assert.That(error.Message,
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
				using (var staticContainer = CreateStaticContainer(x => x.WithSettingsLoader(loadSettings)))
					Assert.That(LocalContainer(staticContainer).Get<SomeService>().Value, Is.EqualTo(87));
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

		public class CanBindFactoryViaServiceConfigurator : ContainerConfigurationTest
		{
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
				public readonly Type ownerType;

				public B(Type ownerType)
				{
					this.ownerType = ownerType;
				}
			}

			public class AConfigurator : IServiceConfigurator<B>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<B> builder)
				{
					builder.Bind(c => new B(c.target));
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var a = container.Get<A>();
				Assert.That(a.b.ownerType, Is.EqualTo(typeof (A)));
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

		public class CanInjectStructViaExplicitConfiguration : BasicTest
		{
			public class A
			{
				public readonly Token token;

				public A(Token token)
				{
					this.token = token;
				}
			}

			public struct Token
			{
				public int value;
			}

			public class TokenSource
			{
				public Token CreateToken()
				{
					return new Token {value = 78};
				}
			}

			public class TokenConfigurator : IServiceConfigurator<Token>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<Token> builder)
				{
					builder.Bind(c => c.container.Get<TokenSource>().CreateToken());
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<A>().token.value, Is.EqualTo(78));
			}
		}

		public class ApplicationNameAndPrimaryAssembly : ContainerConfigurationTest
		{
			public class A
			{
				public A(string applicationName, Assembly primaryAssembly)
				{
					ApplicationName = applicationName;
					PrimaryAssembly = primaryAssembly;
				}

				public string ApplicationName { get; private set; }
				public Assembly PrimaryAssembly { get; private set; }
			}

			public class AConfigurator : IServiceConfigurator<A>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<A> builder)
				{
					builder.Dependencies(new
					{
						applicationName = context.ApplicationName,
						primaryAssembly = context.PrimaryAssembly
					});
				}
			}

			[Test]
			public void Test()
			{
				var staticContainer = CreateStaticContainer();
				using (var c = staticContainer.CreateLocalContainer("my-app-name", Assembly.GetExecutingAssembly(), null, null))
				{
					var a = c.Get<A>();
					Assert.That(a.ApplicationName, Is.EqualTo("my-app-name"));
					Assert.That(a.PrimaryAssembly, Is.SameAs(Assembly.GetExecutingAssembly()));
				}
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

			public class SimpleParametersSource : IParametersSource
			{
				private readonly IDictionary<string, object> values;

				public SimpleParametersSource(IDictionary<string, object> values)
				{
					this.values = values;
				}

				public bool TryGet(string name, Type type, out object value)
				{
					if (!values.TryGetValue(name, out value))
						return false;
					Assert.That(value.GetType(), Is.SameAs(type));
					return true;
				}
			}

			[Test]
			public void Test()
			{
				var parameters = new SimpleParametersSource(new Dictionary<string, object> {{"parameter", 42}});
				var container = Container(parameters: parameters);
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

				var inMemoryContainer = Container(null, typeof (InMemoryProfile));
				Assert.That(inMemoryContainer.Get<IDatabase>(), Is.InstanceOf<InMemoryDatabase>());
			}
		}
	}
}