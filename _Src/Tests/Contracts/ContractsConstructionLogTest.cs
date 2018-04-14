using System;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.Contracts
{
	public abstract class ContractsConstructionLogTest : SimpleContainerTestBase
	{
		public class ContractFormatting : ContractsConstructionLogTest
		{
			public class Wrap
			{
				public readonly Service service;

				public Wrap([TestContract("c1")] Service service)
				{
					this.service = service;
				}
			}

			public class Service
			{
				public readonly SingletonService singletonService;
				public readonly IInterface @interface;

				public Service(SingletonService singletonService, IInterface @interface)
				{
					this.singletonService = singletonService;
					this.@interface = @interface;
				}
			}

			public class SingletonService
			{
			}

			public interface IInterface
			{
			}

			public interface IUnimplemented
			{
			}

			public class Impl1 : IInterface
			{
				public readonly IUnimplemented unimplemented;

				public Impl1(IUnimplemented unimplemented)
				{
					this.unimplemented = unimplemented;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(c => c.Contract("c1").Bind<IInterface, Impl1>());
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<Wrap>());
				var expectedMessage = FormatMessage(@"
no instances for [Wrap] because [IUnimplemented] has no instances
!Wrap
	!Service[c1]
		SingletonService
		!IInterface[c1]
			!Impl1
				!IUnimplemented - has no implementations" + defaultScannedAssemblies);
				Assert.That(error.Message, Is.EqualTo(expectedMessage));
			}
		}

		public class ConstructionLogForSpecificContract : ContractsConstructionLogTest
		{
			public class Wrap
			{
				public readonly A a1;
				public readonly A a2;

				public Wrap([TestContract("a1")] A a1, [TestContract("a2")] A a2)
				{
					this.a1 = a1;
					this.a2 = a2;
				}
			}

			public class A
			{
				public readonly int parameter;

				public A(int parameter)
				{
					this.parameter = parameter;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("a1");
					builder.BindDependency<A>("parameter", 53);
					builder.Contract("a2").BindDependency<A>("parameter", 52);
				});
				container.Get<Wrap>();
				Assert.That(container.Resolve<A>("a2").GetConstructionLog(), Is.EqualTo(FormatMessage(@"
A[a2]
	parameter -> 52")));
			}
		}

		public class ConstructionLogForFactory : ContractsConstructionLogTest
		{
			[TestContract("a")]
			public class A
			{
				public readonly Func<B> func;

				public A(Func<B> func)
				{
					this.func = func;
				}
			}

			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.Contract("a"));
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(FormatMessage(@"
A
	Func<B>")));
			}
		}

		public class DoNotDuplicateRequiredContractsInConstructionLog : ContractsConstructionLogTest
		{
			[TestContract("c1")]
			public class A
			{
				public readonly B b;

				public A([TestContract("c2")] B b)
				{
					this.b = b;
				}
			}

			public class B
			{
				public readonly int parameter;
				public readonly C c;

				public B(int parameter, C c)
				{
					this.parameter = parameter;
					this.c = c;
				}
			}

			public class C
			{
				public readonly int parameter;

				public C(int parameter)
				{
					this.parameter = parameter;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b =>
				{
					b.Contract("c1").Contract("c2").BindDependency<B>("parameter", 14);
					b.Contract("c2").BindDependency<C>("parameter", 55);
				});
				Assert.That(container.Get<A>().b.parameter, Is.EqualTo(14));
				Assert.That(container.Get<A>().b.c.parameter, Is.EqualTo(55));
				Assert.That(container.Resolve<A>().GetConstructionLog(), Does.Contain(FormatMessage(@"
A[c1]
	B[c1->c2]")));
			}
		}
	}
}