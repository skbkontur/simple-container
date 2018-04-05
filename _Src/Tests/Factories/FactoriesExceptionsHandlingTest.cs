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
				var expected = string.Format("many instances for [IB]{0}\tB1{0}\tB2{0}IB++{0}\tB1{0}\tB2{1}",
					Environment.NewLine,
					defaultScannedAssemblies);
				Assert.That(error.Message, Is.EqualTo(expected));
			}
		}

		public class DoNotShowCommentForFactoryErrors : FactoriesExceptionsHandlingTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.GetAll<B>());
				var expected = string.Format("parameter [parameter] of service [A] is not configured{0}{0}!B{0}\tFunc<A>{0}\t!() => A{0}\t\t!parameter <---------------", Environment.NewLine);
				Assert.That(error.Message, Is.EqualTo(expected));
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
				var expected = string.Format("no instances for [IA]{0}!IA - has no implementations{1}",
					Environment.NewLine,
					defaultScannedAssemblies);
				Assert.That(exception.Message, Is.EqualTo(expected));
			}
		}
	}
}