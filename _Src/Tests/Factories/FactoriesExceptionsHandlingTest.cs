using System;
using NUnit.Framework;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.Factories
{
	public abstract class FactoriesExceptionsHandlingTest : SimpleContainerTestBase
	{
		public class FailedResolutionsCommunicatedAsSimpleContainerExceptionOutsideOfConstructor :
			FactoriesExceptionsHandlingTest
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

		public class DoNotShowCommentForFactoryErrors : FactoriesExceptionsHandlingTest
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

		public class CorrectExceptionForUnresolvedService : FactoriesExceptionsHandlingTest
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
				var exception = Assert.Throws<SimpleContainerException>(() => creator(new { parameter = 56 }));
				Assert.That(exception.Message, Is.EqualTo("no instances for [IA]\r\n\r\n!IA - has no implementations"));
			}
		}
	}
}