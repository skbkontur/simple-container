using System;
using System.Text;
using NUnit.Framework;
using SimpleContainer.Infection;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.Factories
{
	public abstract class FactoriesBasicTest : SimpleContainerTestBase
	{
		public class Simple : FactoriesBasicTest
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

		public class ServicesCreatedByFactoriesAreNotSingletons : FactoriesBasicTest
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
				var factory = Container().Get<ServiceC>().factory;
				Assert.That(factory.Invoke(new {someValue = "x"}).someValue, Is.EquivalentTo("x"));
				Assert.That(factory.Invoke(new {someValue = "y"}).someValue, Is.EquivalentTo("y"));
			}
		}

		public class ServicesUsedByServicesCreatedByFactoriesAreSingletons : SimpleContainerTestBase
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
				var factory = Container().Get<ServiceC>().factory;
				Assert.That(factory.Invoke(new {someValue = "x"}).serviceA,
					Is.SameAs(factory.Invoke(new {someValue = "y"}).serviceA));
			}
		}

		public class CloseOverResolutionContextWhenInvokeFromConstructor : FactoriesBasicTest
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
				var a = container.Resolve<A>();
				Assert.That(a.GetConstructionLog(), Is.EqualTo("A\r\n\tFunc<B>\r\n\t() => B\r\n\t\tC\r\n\t() => B"));
				Assert.That(container.Get<B>(), Is.Not.SameAs(a.Single().b1));
				Assert.That(a.Single().b1, Is.Not.SameAs(a.Single().b2));
			}
		}

		public class DoNotCloseOverContextIfFactoryIsInvokedNotFromConstructor : FactoriesBasicTest
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
				var a = container.Resolve<A>();
				a.Single().createB();
				var constructionLog = a.GetConstructionLog();
				Assert.That(constructionLog, Is.EqualTo("A\r\n\tFunc<B>"));
			}
		}


		//public class CanUseCtorDelegate : FactoriesTest
		//{
		//	public class A
		//	{
		//		public readonly B b;
		//		public readonly int someParameter;

		//		private A(B b, int someParameter)
		//		{
		//			this.b = b;
		//			this.someParameter = someParameter;
		//		}

		//		public delegate A Ctor(int someParameter);
		//	}

		//	public class B
		//	{
		//	}

		//	[Test]
		//	public void Test()
		//	{
		//		var container = Container();
		//		var aCtor = container.Get<A.Ctor>();
		//		var instance = aCtor(43);
		//		Assert.That(instance.b, Is.Not.Null);
		//		Assert.That(instance.someParameter, Is.EqualTo(43));
		//	}
		//}

		public class FuncFromFunc : FactoriesBasicTest
		{
			public class A
			{
				public readonly B b;

				public A(Func<B> createB)
				{
					b = createB();
				}
			}

			public class B
			{
				public readonly C c;

				public B(Func<C> createC)
				{
					c = createC();
				}
			}

			public class C
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(() => container.Get<A>(), Throws.Nothing);
			}
		}

		public class FuncFromFuncWithCycle : FactoriesBasicTest
		{
			public class A
			{
				public readonly B b;

				public A(Func<B> createB)
				{
					b = createB();
				}
			}

			public class B
			{
				public readonly A a;

				public B(Func<A> createA)
				{
					a = createA();
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var exception = Assert.Throws<SimpleContainerException>(() => container.Get<A>());
				Assert.That(exception.Message,
					Is.EqualTo(
						"cyclic dependency A ...-> B -> A\r\n\r\n!A\r\n\tFunc<B>\r\n\t!() => B\r\n\t\tFunc<A>\r\n\t\t!() => A <---------------"));
			}
		}

		public class ReusedFactoryInvokationAugmentsItsHostDependencies : FactoriesBasicTest
		{
			public class A
			{
				public readonly Func<C> createC;

				public A(Func<C> createC)
				{
					this.createC = createC;
				}
			}
			
			public class B
			{
				public B(Func<C> createC)
				{
					createC();
				}
			}

			public class C
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				container.Resolve<A>();
				Assert.That(container.Resolve<B>().GetConstructionLog(), Is.EqualTo("B\r\n\tFunc<C>\r\n\t() => C"));
			}
		}

		public class FactoryTakenFromAnyOtherServiceAugmentsItsCurrentHostDependencies : FactoriesBasicTest
		{
			public class A
			{
				public static Func<C> createC;

				public A(Func<C> createC)
				{
					A.createC = createC;
				}
			}

			public class B
			{
				public B()
				{
					A.createC();
					A.createC();
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
				var container = Container(b => b.Contract("x").BindDependency<C>("parameter", 42));
				container.Resolve<A>("x");
				Assert.That(container.Resolve<B>("x", "y").GetConstructionLog(), Is.EqualTo("B\r\n\t() => C[x]\r\n\t\tparameter -> 42\r\n\t() => C[x]"));
			}
		}

		public class ServicesCreatedByConstructorFactoriesAreOwnedByContainer : FactoriesBasicTest
		{
			public class A
			{
				private readonly Func<object, B> createB;

				public A(Func<object, B> createB)
				{
					this.createB = createB;
					createB(new { context = "A.ctor" });
				}

				public void Init()
				{
					createB(new { context = "A.Init" });
				}
			}

			[Lifestyle(Lifestyle.PerRequest)]
			public class B : IDisposable
			{
				public readonly string context;
				public static StringBuilder logBuilder = new StringBuilder();

				public B(string context)
				{
					this.context = context;
				}

				public void Dispose()
				{
					logBuilder.AppendFormat("B.Dispose({0}) ", context);
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var instance = container.Get<A>();
				instance.Init();
				Assert.That(B.logBuilder.ToString(), Is.EqualTo(""));
				container.Dispose();
				Assert.That(B.logBuilder.ToString(), Is.EqualTo("B.Dispose(A.ctor) "));
			}
		}
	}
}