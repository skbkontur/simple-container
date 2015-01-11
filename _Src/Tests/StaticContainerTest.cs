using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Infection;
using SimpleContainer.Interface;

namespace SimpleContainer.Tests
{
	public abstract class StaticContainerTest : SimpleContainerTestBase
	{
		public class StaticServicesReusedBetweenLocalContainers : StaticContainerTest
		{
			[Static]
			public class StaticService
			{
			}

			public class LocalService
			{
				public readonly StaticService staticService;
				public readonly OtherLocalService otherLocalService;

				public LocalService(StaticService staticService, OtherLocalService otherLocalService)
				{
					this.staticService = staticService;
					this.otherLocalService = otherLocalService;
				}
			}

			public class OtherLocalService
			{
			}

			[Test]
			public void Test()
			{
				using (var staticContainer = CreateStaticContainer())
				using (var localContainer1 = LocalContainer(staticContainer, null))
				using (var localContainer2 = LocalContainer(staticContainer, null))
				{
					var localService1 = localContainer1.Get<LocalService>();
					var localService2 = localContainer2.Get<LocalService>();
					Assert.That(localService1, Is.Not.SameAs(localService2));
					Assert.That(localContainer1.Get<LocalService>(), Is.SameAs(localService1));
					Assert.That(localContainer2.Get<LocalService>(), Is.SameAs(localService2));

					Assert.That(localService1.staticService, Is.SameAs(localService2.staticService));
					Assert.That(localService1.otherLocalService, Is.Not.SameAs(localService2.otherLocalService));
				}
			}
		}

		public class CanMakeServiceStaticViaConfiguration : StaticContainerTest
		{
			public class SomeService
			{
			}

			[Static]
			public class StaticConfigurator : IServiceConfigurator<SomeService>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<SomeService> builder)
				{
					builder.MakeStatic();
				}
			}

			[Test]
			public void Test()
			{
				using (var staticContainer = CreateStaticContainer())
				{
					using (var localContainer1 = LocalContainer(staticContainer, _ => { }))
						Assert.That(localContainer1.Get<SomeService>(), Is.SameAs(staticContainer.Get<SomeService>()));
					using (var localContainer2 = LocalContainer(staticContainer, _ => { }))
						Assert.That(localContainer2.Get<SomeService>(), Is.SameAs(staticContainer.Get<SomeService>()));
				}
			}
		}

		public class DisposeStaticServices : StaticContainerTest
		{
			[Static]
			public class StaticService : IDisposable
			{
				public void Dispose()
				{
					LogBuilder.Append("StaticService.Dispose ");
				}
			}

			public class LocalService : IDisposable
			{
				public readonly StaticService staticService;

				public LocalService(StaticService staticService)
				{
					this.staticService = staticService;
				}

				public void Dispose()
				{
					LogBuilder.Append("LocalService.Dispose ");
				}
			}

			[Test]
			public void Test()
			{
				using (var staticContainer = CreateStaticContainer())
				{
					using (var localContainer = LocalContainer(staticContainer, null))
						localContainer.Get<LocalService>();
					Assert.That(LogBuilder.ToString(), Is.EqualTo("LocalService.Dispose "));
					LogBuilder.Clear();
					using (var localContainer = LocalContainer(staticContainer, null))
						localContainer.Get<LocalService>();
					Assert.That(LogBuilder.ToString(), Is.EqualTo("LocalService.Dispose "));
					LogBuilder.Clear();
				}
				Assert.That(LogBuilder.ToString(), Is.EqualTo("StaticService.Dispose "));
			}
		}

		public class DisposeStaticImplementationOfLocalInterface : StaticContainerTest
		{
			[Static]
			public class StaticImpl : IInterface
			{
				public void Dispose()
				{
					LogBuilder.Append("StaticImpl.Dispose ");
				}
			}

			public class LocalImpl : IInterface
			{
				public void Dispose()
				{
					LogBuilder.Append("LocalImpl.Dispose ");
				}
			}

			public interface IInterface : IDisposable
			{
			}

			public class Wrap : IDisposable
			{
				public readonly IEnumerable<IInterface> interfaces;

				public Wrap(IEnumerable<IInterface> interfaces)
				{
					this.interfaces = interfaces;
				}

				public void Dispose()
				{
					LogBuilder.Append("LocalService.Dispose ");
				}
			}

			[Test]
			public void Test()
			{
				using (var staticContainer = CreateStaticContainer())
				{
					using (var localContainer = LocalContainer(staticContainer, null))
					{
						var wrap = localContainer.Get<Wrap>();
						Assert.That(wrap.interfaces.Select(x => x.GetType()).ToArray(),
							Is.EquivalentTo(new[] {typeof (StaticImpl), typeof (LocalImpl)}));
					}
					Assert.That(LogBuilder.ToString(), Is.EqualTo("LocalService.Dispose LocalImpl.Dispose "));
					LogBuilder.Clear();
				}
				Assert.That(LogBuilder.ToString(), Is.EqualTo("StaticImpl.Dispose "));
			}
		}

		public class SimpleStaticConfigurators : StaticContainerTest
		{
			[Static]
			public class A
			{
				public readonly int parameter;

				public A(int parameter)
				{
					this.parameter = parameter;
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

			[Static]
			public class AConfigurator : IServiceConfigurator<A>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<A> builder)
				{
					builder.Dependencies(new {parameter = 42});
				}
			}

			[Static]
			public class BConfigurator : IServiceConfigurator<B>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<B> builder)
				{
					builder.MakeStatic();
					builder.Dependencies(new {parameter = 43});
				}
			}

			[Test]
			public void Test()
			{
				using (var container = CreateStaticContainer())
				{
					Assert.That(container.Get<A>().parameter, Is.EqualTo(42));
					Assert.That(container.Get<B>().parameter, Is.EqualTo(43));
				}
			}
		}

		public class CannotMakeServiceStaticViaNonStaticConfigurator : StaticContainerTest
		{
			public class Service
			{
			}

			public class Configurator : IServiceConfigurator<Service>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<Service> builder)
				{
					builder.MakeStatic();
				}
			}

			[Test]
			public void Test()
			{
				using (var staticContainer = CreateStaticContainer())
				{
					var error = Assert.Throws<SimpleContainerException>(() => LocalContainer(staticContainer, _ => { }));
					Assert.That(error.Message, Is.EqualTo("can't make type [Service] static using non static configurator"));
				}
			}
		}

		public class StaticServiceHasSpecialMarkerInConstructionLog : StaticContainerTest
		{
			[Static]
			public class SomeStaticService
			{
			}

			[Test]
			public void Test()
			{
				var staticContainer = CreateStaticContainer();
				staticContainer.Get<SomeStaticService>();
				var constructionLog = staticContainer.GetConstructionLog(typeof (SomeStaticService));
				Assert.That(constructionLog, Is.EqualTo("(s)SomeStaticService"));
			}
		}

		public class StaticServicesCanUseServicesNotExplicitlyMarkedAsStatic : StaticContainerTest
		{
			[Static]
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
				public readonly int parameter;

				public B(int parameter)
				{
					this.parameter = parameter;
				}
			}

			[Static]
			public class StaticBConfigurator : IServiceConfigurator<B>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<B> builder)
				{
					builder.Dependencies(new {parameter = 41});
				}
			}

			public class NonStaticBConfigurator : IServiceConfigurator<B>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<B> builder)
				{
					builder.Dependencies(new {parameter = 42});
				}
			}

			[Test]
			public void Test()
			{
				using (var staticContainer = CreateStaticContainer())
				{
					var a = staticContainer.Get<A>();
					Assert.That(a.b.parameter, Is.EqualTo(41));
					using (var localContainer = LocalContainer(staticContainer, null))
					{
						var localB = localContainer.Get<B>();
						Assert.That(localB.parameter, Is.EqualTo(42));
					}
				}
			}
		}

		public class CannotConfigureStaticServiceUsingNonStaticConfigurator : StaticContainerTest
		{
			[Static]
			public class B
			{
			}

			public class Configurator : IServiceConfigurator<B>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<B> builder)
				{
					builder.DontUse();
				}
			}

			[Test]
			public void Test()
			{
				using (var staticContainer = CreateStaticContainer())
				{
					var error = Assert.Throws<SimpleContainerException>(() => LocalContainer(staticContainer, _ => { }));
					Assert.That(error.Message, Is.EqualTo("can't configure static service [B] using non static configurator"));
				}
			}
		}
	}
}