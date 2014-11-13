using System.Linq;
using NUnit.Framework;

namespace SimpleContainer.Tests.GenericsConfiguratorTests
{
	public class CanConnectGenericComponents : PreconfiguredContainerTestBase
	{
		public abstract class CommandBase
		{
		}

		public class MyCommand : CommandBase
		{
		}

		public interface IIHandlerWrapper
		{
		}

		public class HandlerWrapper<T> : IIHandlerWrapper
		{
			public readonly IIHandler<T> handler;

			public HandlerWrapper(IIHandler<T> handler)
			{
				this.handler = handler;
			}
		}

		public interface IIHandler
		{
		}

		public interface IIHandler<T> : IIHandler
		{
		}

		public class GenericCommand<T>
		{
		}

		public class GenericHandler<T> : IIHandler<GenericCommand<T>>
			where T : CommandBase
		{
		}

		[Test]
		public void Test()
		{
			var handlers = container.GetAll<IIHandlerWrapper>().ToArray();
			Assert.That(handlers.Length, Is.EqualTo(1));
			Assert.That(handlers[0], Is.TypeOf<HandlerWrapper<GenericCommand<MyCommand>>>());
			Assert.That(((HandlerWrapper<GenericCommand<MyCommand>>) handlers[0]).handler, Is.TypeOf<GenericHandler<MyCommand>>());
		}
	}
}