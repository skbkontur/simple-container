using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class GenericsHandlingTest : SimpleContainerTestBase
	{
		public class Bug : GenericsHandlingTest
		{
			public class A<T>
			{
				public readonly int value;

				public A(int value)
				{
					this.value = value;
				}
			}

			public class B
			{
				public readonly A<int> a;

				public B(A<int> a)
				{
					this.a = a;
				}
			}

			public class AConfigurator : IServiceConfigurator<A<int>>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<A<int>> builder)
				{
					builder.Dependencies(new {value = 42});
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<B>().a.value, Is.EqualTo(42));
			}
		}
	}
}