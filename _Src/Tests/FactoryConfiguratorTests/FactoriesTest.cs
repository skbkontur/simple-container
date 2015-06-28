using System;
using NUnit.Framework;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.FactoryConfiguratorTests
{
	public abstract class FactoriesTest : SimpleContainerTestBase
	{
		public class NewInstanceOfServiceWithUnusedContract : FactoriesTest
		{
			[TestContract("a")]
			public class A
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<A>(), Is.Not.SameAs(container.Create<A>()));
			}
		}

		public class NewInstanceOfServiceWithUnusedContractViaInterface : FactoriesTest
		{
			public class A : IA
			{
			}

			[TestContract("a")]
			public interface IA
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<A>(), Is.Not.SameAs(container.Create<IA>()));
			}
		}

		public class Simple : FactoriesTest
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

		public class CanCreateInterfaceImplementation : FactoriesTest
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

		public class UnusedArguments : FactoriesTest
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
				Assert.That(error.Message, Is.EqualTo("arguments [argument] are not used\r\n\r\n!Service <---------------"));
			}
		}

		public class CloseOverResolutionContextWhenInvokeFromConstructor : FactoriesTest
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
				var constructionLog = a.GetConstructionLog();
				Assert.That(constructionLog, Is.EqualTo("A\r\n\tFunc<B>\r\n\t() => B\r\n\t\tC\r\n\t() => B"));
				Assert.That(container.Get<B>(), Is.Not.SameAs(a.Single().b1));
				Assert.That(a.Single().b1, Is.Not.SameAs(a.Single().b2));
			}
		}

		public class DoNotCloseOverContextIfFactoryIsInvokedNotFromConstructor : FactoriesTest
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

		public class FailedResolutionsCommunicatedAsSimpleContainerExceptionOutsideOfConstructor :
			FactoriesTest
		{
			public class A
			{
				public readonly Func<IB> createB;

				public A(Func<IB> createB)
				{
					this.createB = createB;
				}
			}

			public interface IB
			{
			}

			public class B1 : IB
			{
			}

			public class B2 : IB
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var a = container.Get<A>();
				var error = Assert.Throws<SimpleContainerException>(() => a.createB());
				Assert.That(error.Message, Is.EqualTo("many instances for [IB]\r\n\tB1\r\n\tB2\r\n\r\nIB++\r\n\tB1\r\n\tB2"));
			}
		}

		public class DoNotShowCommentForFactoryErrors : FactoriesTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.GetAll<B>());
				Assert.That(error.Message,
					Is.EqualTo(
						"parameter [parameter] of service [A] is not configured\r\n\r\n!B\r\n\tFunc<A>\r\n\t!() => A\r\n\t\t!parameter <---------------"));
			}

			public class B
			{
				public readonly A createA;

				public B(Func<A> createA)
				{
					this.createA = createA();
					Assert.Fail("must not reach here");
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
		}

		public class ArgumentsAreNotUsedForDependencies : FactoriesTest
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
					Is.EqualTo(
						"parameter [argument] of service [Dependency] is not configured\r\n\r\n!Service\r\n\t!Dependency\r\n\t\t!argument <---------------"));
			}
		}

		public class CanInjectFuncWithArgumentsUsingBuildUp : FactoriesTest
		{
			public class A
			{
			}

			[Inject] private Func<object, A> createA;

			[Test]
			public void Test()
			{
				Container().BuildUp(this, new string[0]);
				Assert.DoesNotThrow(() => createA(new object()));
			}
		}

		public class CorrectExceptionForUnresolvedService : FactoriesTest
		{
			public interface IA
			{
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
				var container = Container();
				var creator = container.Get<Func<object, IA>>();
				var exception = Assert.Throws<SimpleContainerException>(() => creator(new {parameter = 56}));
				Assert.That(exception.Message, Is.EqualTo("no instances for [IA]\r\n\r\n!IA - has no implementations"));
			}
		}

		public class NullValueForFactoryAutoclosingParameter : FactoriesTest
		{
			public interface IA
			{
			}

			public class A<T> : IA
			{
				public readonly T value;

				public A(T value)
				{
					this.value = value;
				}
			}

			public class X
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var creator = container.Get<Func<object, IA>>();
				var exception = Assert.Throws<SimpleContainerException>(() => creator(new {value = (X) null}));
				Assert.That(exception.Message, Is.EqualTo("no instances for [IA]\r\n\r\n!IA - has no implementations"));
			}
		}

		public class CanUseAutoclosingFactoriesInBuildUp : FactoriesTest
		{
			public interface IA
			{
			}

			public class A<T> : IA
			{
				public readonly IS<T> s;

				public A(IS<T> s)
				{
					this.s = s;
				}
			}

			public interface IS<T>
			{
			}

			public class S1<T2> : IS<W<T2>>
			{
			}

			public class W<T>
			{
			}

			[Inject] private Func<object, IA> createA;

			[Test]
			public void Test()
			{
				var container = Container();
				container.BuildUp(this, new string[0]);
				Assert.That(createA(new {s = new S1<int>()}).GetType().FormatName(), Is.EqualTo("A<W<int>>"));
			}
		}

		public class CanInjectFuncWithTypeWithArgumentsUsingBuildUp : FactoriesTest
		{
			public interface IA
			{
				string GetDescription();
			}

			public class A<T> : IA
			{
				private readonly int parameter;

				public A(int parameter)
				{
					this.parameter = parameter;
				}

				public string GetDescription()
				{
					return typeof (T).Name + "_" + parameter;
				}
			}

			[Inject] private Func<object, Type, IA> createA;

			[Test]
			public void Test()
			{
				var container = Container();
				container.BuildUp(this, new string[0]);
				Assert.That(createA(new {parameter = 42}, typeof (A<int>)).GetDescription(), Is.EqualTo("Int32_42"));
			}
		}

		public class CreateWithArgumentThatOverridesConfiguredDependency : BasicTest
		{
			public class A
			{
				public readonly string dependency;

				public A(string dependency)
				{
					this.dependency = dependency;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(builder => builder.BindDependencies<A>(new
				{
					dependency = "configured"
				}));
				var service = container.Create<A>(arguments: new {dependency = "argument"});
				Assert.That(service.dependency, Is.EqualTo("argument"));
			}
		}

		public class FuncFromFunc : BasicTest
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

		//public class CanUseCtorDelegate : BasicTest
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

		public class FuncFromFuncWithCycle : BasicTest
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
				Assert.That(exception.Message, Is.EqualTo("cyclic dependency A ...-> B -> A\r\n\r\n!A\r\n\tFunc<B>\r\n\t!() => B\r\n\t\tFunc<A>\r\n\t\t!() => A <---------------"));
			}
		}
	}
}