using NUnit.Framework;
using SimpleContainer.Configuration;

namespace SimpleContainer.Tests
{
	public abstract class GetImplementationsOfTest : SimpleContainerTestBase
	{
		public class DontUseIsTakenIntoAccountWhenDetectingImplementations : GetImplementationsOfTest
		{
			[Test]
			public void Test()
			{
				var container = Container(c => c.DontUse(typeof (B)));
				Assert.That(container.GetImplementationsOf<IIntf>(), Is.EquivalentTo(new[] {typeof (A)}));
			}

			public class A : IIntf
			{
			}

			public class B : IIntf
			{
			}

			public interface IIntf
			{
			}
		}

		public class ExplicitlyConfiguredImplementations : GetImplementationsOfTest
		{
			public interface IInterface
			{
			}

			public class A : IInterface
			{
			}

			public class B : IInterface
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder builder)
				{
					builder.Bind<IInterface, B>();
					builder.Bind<IInterface, A>();
				});
				Assert.That(container.GetImplementationsOf<IInterface>(), Is.EqualTo(new[] {typeof (B), typeof (A)}));
			}
		}
	}
}