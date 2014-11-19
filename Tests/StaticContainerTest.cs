using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SimpleContainer.Implementation;
using SimpleContainer.Infection;

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

		public class StaticServiceCannotReferenceLocalService : StaticContainerTest
		{
			[Static]
			public class StaticService
			{
				public readonly LocalService localService;

				public StaticService(LocalService localService)
				{
					this.localService = localService;
				}
			}

			public class LocalService
			{
			}

			[Test]
			public void Test()
			{
				using (var staticContainer = CreateStaticContainer())
				{
					var error = Assert.Throws<SimpleContainerException>(() => staticContainer.Get<StaticService>());
					Assert.That(error.Message,
						Is.EqualTo("local service [LocalService] can't be resolved in static context\r\nStaticService!"));
				}
			}
		}

		public class CanMakeServiceStaticViaConfiguration : StaticContainerTest
		{
			public class SomeService
			{
			}

			[Test]
			public void Test()
			{
				using (var staticContainer = CreateStaticContainer())
				{
					using (
						var localContainer1 = LocalContainer(staticContainer, b => b.CacheLevel(typeof (SomeService), CacheLevel.Static)))
						Assert.That(localContainer1.Get<SomeService>(), Is.SameAs(staticContainer.Get<SomeService>()));
					using (
						var localContainer2 = LocalContainer(staticContainer, b => b.CacheLevel(typeof (SomeService), CacheLevel.Static)))
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

		public class StaticConfigurationMustBeTheSameBetweenLocalContainers : StaticContainerTest
		{
			public class StaticService
			{
			}

			public class LocalService
			{
			}

			[Test]
			public void WereStaticNowLocal()
			{
				using (var staticContainer = CreateStaticContainer())
				{
					using (
						var localContainer1 = LocalContainer(staticContainer, b => b.CacheLevel(typeof (StaticService), CacheLevel.Static))
						)
						Assert.That(localContainer1.Get<StaticService>(), Is.SameAs(staticContainer.Get<StaticService>()));

					var error = Assert.Throws<SimpleContainerException>(() => LocalContainer(staticContainer,
						b => b.CacheLevel(typeof (StaticService), CacheLevel.Local)));
					Assert.That(error.Message, Is.EqualTo("inconsistent static configuration, [StaticService] were static, now local"));
				}
			}

			[Test]
			public void WereLocalNowStatic()
			{
				using (var staticContainer = CreateStaticContainer())
				{
					using (
						var localContainer1 = LocalContainer(staticContainer, b => b.CacheLevel(typeof (StaticService), CacheLevel.Static))
						)
						Assert.That(localContainer1.Get<StaticService>(), Is.SameAs(staticContainer.Get<StaticService>()));

					var error = Assert.Throws<SimpleContainerException>(() => LocalContainer(staticContainer,
						b =>
						{
							b.CacheLevel(typeof (StaticService), CacheLevel.Static);
							b.CacheLevel(typeof (LocalService), CacheLevel.Static);
						}));
					Assert.That(error.Message, Is.EqualTo("inconsistent static configuration, [LocalService] were local, now static"));
				}
			}

			[Test]
			public void StaticOnContractLevel()
			{
				using (var staticContainer = CreateStaticContainer())
				{
					using (
						var localContainer1 = LocalContainer(staticContainer, b => b.CacheLevel(typeof (StaticService), CacheLevel.Static))
						)
						Assert.That(localContainer1.Get<StaticService>(), Is.SameAs(staticContainer.Get<StaticService>()));

					var error = Assert.Throws<SimpleContainerException>(() => LocalContainer(staticContainer,
						b =>
						{
							b.CacheLevel(typeof (StaticService), CacheLevel.Static);
							b.Contract("test").CacheLevel(typeof (LocalService), CacheLevel.Static);
						}));
					Assert.That(error.Message,
						Is.EqualTo("can't configure static on contract level; contract [test], services [LocalService]"));
				}
			}
		}
	}
}