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
				Assert.That(error.Message, Is.EqualTo(TestHelpers.FormatMessage(@"
many instances for [IB]
	B1
	B2
IB++
	B1
	B2" + defaultScannedAssemblies)));
			}
		}

		public class DoNotShowCommentForFactoryErrors : FactoriesExceptionsHandlingTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.GetAll<B>());
				Assert.That(error.Message, Is.EqualTo(TestHelpers.FormatMessage(@"
parameter [parameter] of service [A] is not configured

!B
	Func<A>
	!() => A
		!parameter <---------------")));
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
				Assert.That(exception.Message, Is.EqualTo(TestHelpers.FormatMessage(@"
no instances for [IA]
!IA - has no implementations" + defaultScannedAssemblies)));
			}
		}
	}
}