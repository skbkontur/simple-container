using System;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Infection;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class IgnoreImplementationTest : SimpleContainerTestBase
	{
		public class CorrectConstructionLogForExplicitlyIgnoredImplementation : IgnoreImplementationTest
		{
			public class A
			{
				public readonly IB b;

				public A(IB b)
				{
					this.b = b;
				}
			}

			public interface IB
			{
			}

			[IgnoredImplementation]
			public class B : IB
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var exception = Assert.Throws<SimpleContainerException>(() => container.Get<A>());
				Assert.That(exception.Message, Is.EqualTo("no instances for [A] because [IB] has no instances\r\n\r\n!A\r\n\t!IB\r\n\t\t!B - IgnoredImplementation" + defaultScannedAssemblies));
			}
		}

	    public class GracefullyHandleCrashInImplementationConfigurator : IgnoreImplementationTest
	    {
	        public interface IA
	        {
	        }

	        public class A : IA
	        {
	        }

	        public class AConfigurator : IServiceConfigurator<A>
	        {
	            public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<A> builder)
	            {
	                throw new InvalidOperationException("test-crash");
	            }
	        }

	        private static void CheckException(SimpleContainerException e)
	        {
                Assert.That(e.Message, Is.EqualTo(FormatExpectedMessage(@"
service [IA] construction exception

!IA <---------------")));
	            Assert.That(e.InnerException.Message, Is.EqualTo("error executing configurator [AConfigurator]"));
	            Assert.That(e.InnerException.InnerException.Message, Is.EqualTo("test-crash"));
	        }

	        [Test]
	        public void Test()
	        {
	            var container = Container();
                CheckException(Assert.Throws<SimpleContainerException>(() => container.Get<IA>()));
                //check no side effects because of unexpected stack unwind
                CheckException(Assert.Throws<SimpleContainerException>(() => container.Get<IA>()));
	        }
	    }

		public class CanExplicitlyInjectIgnoredImplementation : IgnoreImplementationTest
		{
			[IgnoredImplementation]
			public class A
			{
			}

			public class AWrap
			{
				public readonly A a;

				public AWrap(A a)
				{
					this.a = a;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(() => container.Get<AWrap>(), Throws.Nothing);
			}
		}

		public class CanIgnoreImplementationFromBuilder : IgnoreImplementationTest
		{
			public interface IA
			{
			}

			public class A : IA
			{
			}

			public class AWrap
			{
				public readonly A a;
				public readonly IA aIntf;

				public AWrap(A a, IA aIntf = null)
				{
					this.a = a;
					this.aIntf = aIntf;
				}
			}

			public class AConfigurator : IServiceConfigurator<A>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<A> builder)
				{
					builder.IgnoreImplementation();
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var aWrap = container.Get<AWrap>();
				Assert.That(aWrap.a, Is.Not.Null);
				Assert.That(aWrap.aIntf, Is.Null);
			}
		}

		public class DependencyOnIgnoredAndNotIgnoredImplementations : IgnoreImplementationTest
		{
			public interface IA
			{
			}

			[IgnoredImplementation]
			public class A1 : IA
			{
			}

			public class A2 : IA
			{
			}

			public class B
			{
				public readonly IA a;

				public B(IA a)
				{
					this.a = a;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<B>().a, Is.InstanceOf<A2>());
			}
		}

		public class DontUseIgnoredImplementationAttributeForExplicitConfiguration : IgnoreImplementationTest
		{
			public interface IA
			{
			}

			public class A1 : IA
			{
			}

			[IgnoredImplementation]
			public class A2 : IA
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.Bind<IA, A2>());
				Assert.That(container.Get<IA>(), Is.InstanceOf<A2>());
			}
		}

		public class IgnoreImplementation : IgnoreImplementationTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<IInterface>(), Is.InstanceOf<Impl1>());
			}

			public interface IInterface
			{
			}

			public class Impl1 : IInterface
			{
			}

			[Infection.IgnoredImplementation]
			public class Impl2 : IInterface
			{
			}

			public class IgnoredImplementationAttribute : Attribute
			{
			}

			[IgnoredImplementation]
			public class Impl3 : IInterface
			{
			}
		}

		public class IgnoreImplementationAffectsOnlyInterfaces : IgnoreImplementationTest
		{
			public interface IA
			{
			}

			[IgnoredImplementation]
			public class A1 : IA
			{
			}

			public class A2 : IA
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<IA>(), Is.InstanceOf<A2>());
				var resolvedA1 = container.Resolve<A1>();
				Assert.That(resolvedA1.IsOk());
				Assert.That(resolvedA1.Single(), Is.Not.Null);
				Assert.That(resolvedA1.GetConstructionLog(), Is.EqualTo("A1"));
			}
		}
	}
}