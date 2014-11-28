using NUnit.Framework;

namespace SimpleContainer.Tests
{
	public abstract class NotConfiguredGenericsTest : SimpleContainerTestBase
	{
		public class Simple : NotConfiguredGenericsTest
		{
			public class Generic<T>
			{
			}

			public class GenericClient
			{
				public readonly Generic<A> generic;

				public GenericClient(Generic<A> generic)
				{
					this.generic = generic;
				}
			}

			public class A
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<GenericClient>().generic, Is.Not.Null);
			}
		}

		//todo
		//public class SimpleWithInterfaces : NotConfiguredGenericsTest
		//{
		//	public class Generic<T> : IGenericInterface<T>
		//	{
		//	}

		//	public interface IGenericInterface<T>
		//	{
		//	}

		//	public class GenericClient
		//	{
		//		public readonly IGenericInterface<A> generic;

		//		public GenericClient(IGenericInterface<A> generic)
		//		{
		//			this.generic = generic;
		//		}
		//	}

		//	public class A
		//	{
		//	}

		//	[Test]
		//	public void Test()
		//	{
		//		var container = Container();
		//		Assert.That(container.Get<GenericClient>().generic, Is.InstanceOf<Generic<A>>());
		//	}
		//}
	}
}