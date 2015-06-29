using System;
using NUnit.Framework;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.Factories
{
	public abstract class FactoriesGenericHandlingTest : SimpleContainerTestBase
	{
		public class CanUseAutoclosingFactoriesInBuildUp : FactoriesGenericHandlingTest
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

			[Inject]
			private Func<object, IA> createA;

			[Test]
			public void Test()
			{
				var container = Container();
				container.BuildUp(this, new string[0]);
				Assert.That(createA(new { s = new S1<int>() }).GetType().FormatName(), Is.EqualTo("A<W<int>>"));
			}
		}

		public class CanAutodetectGenericBySimpleGenericParameter : FactoriesGenericHandlingTest
		{
			public class SomeOuterService
			{
				private readonly Func<object, ISomeService> factory;

				public SomeOuterService(Func<object, ISomeService> factory)
				{
					this.factory = factory;
				}

				public ISomeService Create(object item)
				{
					return factory(new { item });
				}

				public class SomeService<T> : ISomeService
				{
					public T Item { get; private set; }

					public SomeService(T item)
					{
						Item = item;
					}
				}
			}

			public interface ISomeService
			{
			}


			[Test]
			public void Test()
			{
				var someOuterService = Container().Get<SomeOuterService>();
				var someService = someOuterService.Create(23);
				var typedSomeService = someService as SomeOuterService.SomeService<int>;
				Assert.That(typedSomeService, Is.Not.Null);
				Assert.That(typedSomeService.Item, Is.EqualTo(23));
			}
		}

		public class CanAutodetectGenericArgumentType : FactoriesGenericHandlingTest
		{
			public class SomeService
			{
				private readonly Func<object, IGenericServiceDecorator> getDecorator;

				public SomeService(Func<object, IGenericServiceDecorator> getDecorator)
				{
					this.getDecorator = getDecorator;
				}

				public string Run(object genericService, string parameter)
				{
					return getDecorator(new { genericService, parameter }).DoSomething();
				}

				public class GenericServiceDecorator<T> : IGenericServiceDecorator
				{
					private readonly IGenericService<T> genericService;
					private readonly string parameter;

					public GenericServiceDecorator(IGenericService<T> genericService, string parameter)
					{
						this.genericService = genericService;
						this.parameter = parameter;
					}

					public string DoSomething()
					{
						return genericService.Describe() + " " + parameter;
					}
				}
			}

			public interface IGenericServiceDecorator
			{
				string DoSomething();
			}

			public interface IGenericService<T>
			{
				string Describe();
			}

			public class IntGenericService : IGenericService<int>
			{
				public string Describe()
				{
					return "i'm int";
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var someService = container.Get<SomeService>();
				var intGenericService = container.Get<IntGenericService>();
				Assert.That(someService.Run(intGenericService, "testParameter"), Is.EqualTo("i'm int testParameter"));
			}
		}

		public class NullValueForFactoryAutoclosingParameter : FactoriesGenericHandlingTest
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
				var exception = Assert.Throws<SimpleContainerException>(() => creator(new { value = (X)null }));
				Assert.That(exception.Message, Is.EqualTo("no instances for [IA]\r\n\r\n!IA - has no implementations"));
			}
		}

		public class CanCreateConcreteGenericImplementations : FactoriesGenericHandlingTest
		{
			public interface IServiceB
			{
			}

			public class ServiceB<T> : IServiceB
			{
				public readonly int parameter;

				public ServiceB(int parameter)
				{
					this.parameter = parameter;
				}
			}

			public class ServiceA
			{
				public readonly Func<Type, object, IServiceB> func;

				public ServiceA(Func<Type, object, IServiceB> func)
				{
					this.func = func;
				}
			}

			[Test]
			public void Test()
			{
				var serviceA = Container().Get<ServiceA>();
				var serviceB = serviceA.func(typeof(ServiceB<int>), new { parameter = 42 });
				Assert.That(serviceB, Is.InstanceOf<ServiceB<int>>());
				Assert.That(((ServiceB<int>)serviceB).parameter, Is.EqualTo(42));
			}
		}
	}
}