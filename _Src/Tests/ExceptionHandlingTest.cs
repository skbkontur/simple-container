using NUnit.Framework;
using SimpleContainer.Infection;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class ExceptionHandlingTest : SimpleContainerTestBase
	{
		public class GracefullErrorMessageWhenNoImplementationFound : BasicTest
		{
			[Test]
			public void Test()
			{
				const string message =
					"no instances for [OuterOuterService] because [IInterface] has no instances\r\n\r\n!OuterOuterService\r\n\t!OuterService\r\n\t\t!IInterface - has no implementations" + defaultScannedAssemblies;
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<OuterOuterService>());
				Assert.That(error.Message, Is.EqualTo(message));
			}

			public interface IInterface
			{
			}

			public class OuterOuterService
			{
				public OuterOuterService(OuterService outerService)
				{
					OuterService = outerService;
				}

				public OuterService OuterService { get; private set; }
			}

			public class OuterService
			{
				public OuterService(IInterface @interface)
				{
					Interface = @interface;
				}

				public IInterface Interface { get; private set; }
			}
		}

		public class UnwindNotResolvedToTheRootCause : ExceptionHandlingTest
		{
			public class A
			{
				public readonly B b;

				public A(B b)
				{
					this.b = b;
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

			[DontUse]
			public class C
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<A>());
				Assert.That(error.Message, Is.EqualTo("no instances for [A] because [C] has no instances\r\n\r\n!A\r\n\t!B\r\n\t\t!C - DontUse" + defaultScannedAssemblies));
			}
		}

		public class UnwindNotResolvedInterfaces : ExceptionHandlingTest
		{
			public class A
			{
				public readonly B b;

				public A(B b)
				{
					this.b = b;
				}
			}

			public class B
			{
				public readonly IC c;

				public B(IC c)
				{
					this.c = c;
				}
			}

			public interface IC
			{
			}

			[DontUse]
			public class C1 : IC
			{
			}

			[DontUse]
			public class C2 : IC
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<A>());
				Assert.That(error.Message, Is.EqualTo("no instances for [A] because [IC] has no instances\r\n\r\n!A\r\n\t!B\r\n\t\t!IC\r\n\t\t\t!C1 - DontUse\r\n\t\t\t!C2 - DontUse" + defaultScannedAssemblies));
			}
		}
	}
}