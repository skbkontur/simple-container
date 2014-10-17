using System.Linq;
using NUnit.Framework;

namespace SimpleContainer.Tests.GenericsConfiguratorTests
{
	public class CheckGenericAttributesWhenDeducingTypeFromConstraintsTest : GenericConfigurationTestBase
	{
		public interface IMyCommand
		{
		}

		public class MyCommand1 : IMyCommand
		{
		}

		public class MyCommand2 : IMyCommand
		{
			public int Value { get; set; }

			public MyCommand2(int value)
			{
				Value = value;
			}
		}

		public interface IHandler
		{
		}

		public class Handler<T> : IHandler
			where T : IMyCommand, new()
		{
		}

		[Test]
		public void Test()
		{
			Assert.That(container.GetAll<IHandler>().Select(x => x.GetType()).ToArray(), Is.EqualTo(new[] {typeof (Handler<MyCommand1>)}));
		}
	}
}