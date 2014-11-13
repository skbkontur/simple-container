using System.Linq;
using NUnit.Framework;

namespace SimpleContainer.Tests.GenericsConfiguratorTests
{
	public class CanDeduceGenericsFromConstraintsTest : PreconfiguredContainerTestBase
	{
		public interface IMyCommand
		{
		}

		public class MyCommand1 : IMyCommand
		{
		}

		public class MyCommand2 : IMyCommand
		{
		}

		public interface IHandler
		{
		}

		public class Handler<T> : IHandler
			where T : IMyCommand
		{
		}

		[Test]
		public void Test()
		{
			Assert.That(container.GetAll<IHandler>().Select(x => x.GetType()).ToArray(),
			            Is.EquivalentTo(new[] {typeof (Handler<MyCommand1>), typeof (Handler<MyCommand2>)}));
		}
	}
}