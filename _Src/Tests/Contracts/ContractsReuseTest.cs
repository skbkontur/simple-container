using System;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Infection;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.Contracts
{
	public abstract class ContractsReuseTest : SimpleContainerTestBase
	{
		public class ContractUsageViaCache : ContractsReuseTest
		{
			public class Client
			{
				public readonly ServiceWrap wrap;
				public readonly OtherService otherService;

				public Client([TestContract("c1")] ServiceWrap wrap, OtherService otherService)
				{
					this.wrap = wrap;
					this.otherService = otherService;
				}
			}

			public class ServiceWrap
			{
				public readonly Service service;
				public readonly OtherService otherService;

				public ServiceWrap(Service service, OtherService otherService)
				{
					this.service = service;
					this.otherService = otherService;
				}
			}

			public class OtherService
			{
				public readonly Service service;

				public OtherService(Service service)
				{
					this.service = service;
				}
			}

			public class Service
			{
				public readonly IInterface @interface;

				public Service(IInterface @interface)
				{
					this.@interface = @interface;
				}
			}

			public interface IInterface
			{
			}

			public class Impl : IInterface
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(c => c.Contract("c1").Bind<IInterface, Impl>());
				var client = container.Get<Client>();
				Assert.That(client.wrap.otherService, Is.Not.SameAs(client.otherService));
			}
		}

		public class UsedContractsForReusedServices : ContractsReuseTest
		{
			public class A
			{
				public readonly B b;
				public readonly D d1;
				public readonly D d2;

				public A([TestContract("c1")] B b, [TestContract("c2")] D d1, D d2)
				{
					this.b = b;
					this.d1 = d1;
					this.d2 = d2;
				}
			}

			public class B
			{
				public readonly C c;

				public B([TestContract("c2")] C c)
				{
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

			public class D
			{
				public readonly C c;

				public D(C c)
				{
					this.c = c;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.BindDependency<C>("parameter", 41);
					builder.Contract("c2").BindDependency<C>("parameter", 42);
				});
				var a = container.Get<A>();
				Assert.That(a.b.c.parameter, Is.EqualTo(42));
				Assert.That(a.d1.c, Is.SameAs(a.b.c));
				Assert.That(a.d2.c.parameter, Is.EqualTo(41));
			}
		}

		public class UsedContractsForServiceCreatedUsingFinalContracts : ContractsReuseTest
		{
			public class A
			{
				public readonly Wrap wrap;
				public readonly C c1;
				public readonly C c2;

				public A([TestContract("x")] Wrap wrap, [TestContract("x")] C c1, C c2)
				{
					this.wrap = wrap;
					this.c1 = c1;
					this.c2 = c2;
				}
			}

			[TestContract("not-used")]
			public class Wrap
			{
				public readonly B b;

				public Wrap(B b)
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

			public class C
			{
				public readonly B b;

				public C(B b)
				{
					this.b = b;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("not-used");
					builder.BindDependency<B>("parameter", 1);
					builder.Contract("x").BindDependency<B>("parameter", 2);
				});
				var a = container.Get<A>();
				Assert.That(a.wrap.b.parameter, Is.EqualTo(2));
				Assert.That(a.c1.b.parameter, Is.EqualTo(2));
				Assert.That(a.c2.b.parameter, Is.EqualTo(1));
			}
		}

		public class ContractsSequenceIsImportant : ContractsReuseTest
		{
			public class A
			{
				public readonly B b;
				public readonly C c;

				public A([TestContract("x")] B b, [TestContract("y")] C c)
				{
					this.b = b;
					this.c = c;
				}
			}

			[TestContract("not-used")]
			public class B
			{
				public readonly U u;

				public B([TestContract("y")] U u)
				{
					this.u = u;
				}
			}

			public class C
			{
				public readonly U u;

				public C([TestContract("x")] U u)
				{
					this.u = u;
				}
			}

			public class U
			{
				public readonly IInterface s;

				public U(IInterface s)
				{
					this.s = s;
				}
			}

			public interface IInterface
			{
			}

			public class Impl1 : IInterface
			{
				public readonly int parameter;

				public Impl1(int parameter)
				{
					this.parameter = parameter;
				}
			}

			public class Impl2 : IInterface
			{
				public readonly int parameter;

				public Impl2(int parameter)
				{
					this.parameter = parameter;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Contract("not-used");
					builder.Contract("x")
						.Bind<IInterface, Impl2>()
						.BindDependency<Impl1>("parameter", 1);

					builder.Contract("y")
						.BindDependency<Impl2>("parameter", 2)
						.Bind<IInterface, Impl1>();
				});

				var a = container.Get<A>();
				Assert.That(a.b.u.s, Is.InstanceOf<Impl1>());
				Assert.That(((Impl1)a.b.u.s).parameter, Is.EqualTo(1));

				Assert.That(a.c.u.s, Is.InstanceOf<Impl2>());
				Assert.That(((Impl2)a.c.u.s).parameter, Is.EqualTo(2));
			}
		}


		public class FinalUsedContractsForSkippedServices : ContractsReuseTest
		{
			public class Wrap
			{
				public readonly A a;

				public Wrap([TestContract("x")] A a)
				{
					this.a = a;
				}
			}

			public class A
			{
				public readonly B b;

				public A([Optional] B b)
				{
					this.b = b;
				}
			}

			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.Contract("x").DontUse<B>());
				Assert.That(container.Get<Wrap>().a.b, Is.Null);
				Assert.That(container.Resolve<A>("x").GetConstructionLog(), Is.EqualTo(FormatMessage(@"
A[x]
	B[x] - DontUse -> <null>")));
			}
		}

		public class ConstructionLogForReusedService : ContractsReuseTest
		{
			public class A
			{
				public readonly B b1;
				public readonly B b2;

				public A([TestContract("x")] B b1, [TestContract("y")] B b2)
				{
					this.b1 = b1;
					this.b2 = b2;
				}
			}

			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(b =>
				{
					b.Contract("x");
					b.Contract("y");
				});
				var a = container.Get<A>();
				Assert.That(a.b1, Is.SameAs(a.b2));
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(FormatMessage(@"
A
	B
	B")));
			}
		}

		public class CrashInConstructorOfServiceForUsedContracts : ContractsReuseTest
		{
			public class A
			{
				public A()
				{
					throw new InvalidOperationException("test crash");
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.Contract("a"));
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<A>("a"));
				Assert.That(error.Message, Does.Contain("construction exception"));
				Assert.That(error.InnerException.Message, Is.EqualTo("test crash"));
			}
		}

		public class ServicesAreBoundToUsedContractPath : ContractsReuseTest
		{
			public class A
			{
				public readonly B b;
				public readonly C c;

				public A([TestContract("x1")] B b, [TestContract("x2")] C c)
				{
					this.b = b;
					this.c = c;
				}
			}

			public class B
			{
				public readonly C c;

				public B([TestContract("x2")] C c)
				{
					this.c = c;
				}
			}

			public class C
			{
				public readonly int p;

				public C(int p)
				{
					this.p = p;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b =>
				{
					b.Contract("x1");
					b.Contract("x2").BindDependency<C>("p", 42);
				});
				var a = container.Get<A>();
				Assert.That(a.b.c, Is.SameAs(a.c));
			}
		}
	}
}