using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using SimpleContainer.Implementation;
using SimpleContainer.Interface;

namespace SimpleContainer.Tests.FactoryConfiguratorTests
{
	public abstract class SimpleFactoryConfiguratorTest : SimpleContainerTestBase
	{
		public class Simple : SimpleFactoryConfiguratorTest
		{
			public class ServiceA
			{
			}

			public class ServiceB
			{
				public readonly ServiceA serviceA;
				public readonly string someValue;

				public ServiceB(ServiceA serviceA, string someValue)
				{
					this.serviceA = serviceA;
					this.someValue = someValue;
				}
			}

			public class ServiceC
			{
				public readonly Func<object, ServiceB> factory;

				public ServiceC(Func<object, ServiceB> factory)
				{
					this.factory = factory;
				}
			}

			[Test]
			public void Test()
			{
				var service = Container().Get<ServiceC>().factory.Invoke(new {someValue = "x"});
				Assert.That(service.serviceA, Is.Not.Null);
				Assert.That(service.someValue, Is.EquivalentTo("x"));
			}
		}

		public class CanCreateInterfaceImplementation : SimpleFactoryConfiguratorTest
		{
			public interface IInterface
			{
			}

			public class Impl : IInterface
			{
				public readonly string argument;

				public Impl(string argument)
				{
					this.argument = argument;
				}
			}

			public class Wrap
			{
				public readonly Func<object, IInterface> createInterface;

				public Wrap(Func<object, IInterface> createInterface)
				{
					this.createInterface = createInterface;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var impl = container.Get<Wrap>().createInterface(new {argument = "666"});
				Assert.That(impl, Is.InstanceOf<Impl>());
				Assert.That(((Impl) impl).argument, Is.EqualTo("666"));
			}
		}

		public class UnusedArguments : SimpleFactoryConfiguratorTest
		{
			public class Wrap
			{
				public readonly Func<object, Service> createService;

				public Wrap(Func<object, Service> createService)
				{
					this.createService = createService;
				}
			}

			public class Service
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var wrap = container.Get<Wrap>();
				var error = Assert.Throws<SimpleContainerException>(() => wrap.createService(new {argument = "qq"}));
				Assert.That(error.Message, Is.EqualTo("arguments [argument] are not used\r\nService"));
			}
		}

		public class CloseOverResolutionContextWhenInvokeFromConstructor : SimpleFactoryConfiguratorTest
		{
			public class A
			{
				public B b1;
				public B b2;

				public A(Func<B> createB)
				{
					b1 = createB();
					b2 = createB();
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
				var container = Container();
				var a = container.Get<A>();
				var constructionLog = container.GetConstructionLog(typeof (A));
				Assert.That(constructionLog, Is.EqualTo("A\r\n\tFunc<B>\r\n\tB\r\n\t\tC\r\n\tB"));
				Assert.That(container.Get<B>(), Is.Not.SameAs(a.b1));
				Assert.That(a.b1, Is.Not.SameAs(a.b2));
			}
		}

		public class DoNotCloseOverContextIfFactoryIsInvokedNotFromConstructor : SimpleFactoryConfiguratorTest
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

			public class C
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var a = container.Get<A>();
				a.createB();
				var constructionLog = container.GetConstructionLog(typeof (A), null, true);
				Assert.That(constructionLog, Is.EqualTo("A\r\n\tFunc<B>"));
			}
		}

		public class ArgumentsAreNotUsedForDependencies : SimpleFactoryConfiguratorTest
		{
			public class Wrap
			{
				public readonly Func<object, Service> createService;

				public Wrap(Func<object, Service> createService)
				{
					this.createService = createService;
				}
			}

			public class Service
			{
				public readonly Dependency dependency;

				public Service(Dependency dependency)
				{
					this.dependency = dependency;
				}
			}

			public class Dependency
			{
				public readonly string argument;

				public Dependency(string argument)
				{
					this.argument = argument;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var wrap = container.Get<Wrap>();
				var error = Assert.Throws<SimpleContainerException>(() => wrap.createService(new {argument = "qq"}));
				Assert.That(error.Message,
					Is.EqualTo("can't create simple type\r\nService!\r\n\tDependency!\r\n\t\targument! - <---------------"));
			}
		}
	}
}