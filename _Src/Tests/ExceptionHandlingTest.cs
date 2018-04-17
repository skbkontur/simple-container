using System;
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
				var message = TestHelpers.FormatMessage(@"
no instances for [OuterOuterService] because [IInterface] has no instances
!OuterOuterService
	!OuterService
		!IInterface - has no implementations" + defaultScannedAssemblies);
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
				Assert.That(error.Message, Is.EqualTo(TestHelpers.FormatMessage(@"
no instances for [A] because [C] has no instances
!A
	!B
		!C - DontUse" + defaultScannedAssemblies)));
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
				Assert.That(error.Message, Is.EqualTo(TestHelpers.FormatMessage(@"
no instances for [A] because [IC] has no instances
!A
	!B
		!IC
			!C1 - DontUse
			!C2 - DontUse" + defaultScannedAssemblies)));
			}
		}
	}
}