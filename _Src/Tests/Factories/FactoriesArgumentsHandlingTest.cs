using System;
using NUnit.Framework;
using SimpleContainer.Infection;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.Factories
{
	public abstract class FactoriesArgumentsHandlingTest : SimpleContainerTestBase
	{
		public class UnusedArguments : FactoriesArgumentsHandlingTest
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
				var error = Assert.Throws<SimpleContainerException>(() => wrap.createService(new { argument = "qq" }));
				Assert.That(error.Message, Is.EqualTo("arguments [argument] are not used\r\n\r\n!Service <---------------"));
			}
		}

		public class PassArgumentsFromInterfaceToImplmementation : FactoriesArgumentsHandlingTest
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
				var impl = container.Get<Wrap>().createInterface(new { argument = "666" });
				Assert.That(impl, Is.InstanceOf<Impl>());
				Assert.That(((Impl)impl).argument, Is.EqualTo("666"));
			}
		}

		public class ArgumentsAreNotUsedForDependencies : FactoriesArgumentsHandlingTest
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
				var error = Assert.Throws<SimpleContainerException>(() => wrap.createService(new { argument = "qq" }));
				Assert.That(error.Message,
					Is.EqualTo(
						"parameter [argument] of service [Dependency] is not configured\r\n\r\n!Service\r\n\t!Dependency\r\n\t\t!argument <---------------"));
			}
		}

		public class CanInjectFuncWithArgumentsUsingBuildUp : FactoriesArgumentsHandlingTest
		{
			public class A
			{
			}

			[Inject]
			private Func<object, A> createA;

			[Test]
			public void Test()
			{
				Container().BuildUp(this, new string[0]);
				Assert.DoesNotThrow(() => createA(new object()));
			}
		}

		public class CreateWithArgumentThatOverridesConfiguredDependency : FactoriesArgumentsHandlingTest
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
				var service = container.Create<A>(arguments: new { dependency = "argument" });
				Assert.That(service.dependency, Is.EqualTo("argument"));
			}
		}

		public class GracefullErrorForNotSupportedDelegateTypes : FactoriesArgumentsHandlingTest
		{
			public class A
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var exception = Assert.Throws<SimpleContainerException>(() => container.Get<Func<int, int, A>>());
				Assert.That(exception.Message,
					Is.EqualTo("can't create delegate [Func<int,int,A>]\r\n\r\n!Func<int,int,A> <---------------"));
			}
		}

		public class CanInjectFuncWithTypeWithArgumentsUsingBuildUp : FactoriesArgumentsHandlingTest
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
					return typeof(T).Name + "_" + parameter;
				}
			}

			[Inject]
			private Func<object, Type, IA> createA;

			[Test]
			public void Test()
			{
				var container = Container();
				container.BuildUp(this, new string[0]);
				Assert.That(createA(new { parameter = 42 }, typeof(A<int>)).GetDescription(), Is.EqualTo("Int32_42"));
			}
		}
	}
}