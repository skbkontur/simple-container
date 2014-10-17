using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace SimpleContainer.Tests.GenericsConfiguratorTests
{
	public class CanConfigureInnerGenericsTest: GenericConfigurationTestBase
	{
		public interface IInterface
		{
		}

		public interface IInterface<T> : IInterface
		{
		}

		public class JoinEvent<T1, T2>
		{
		}

		public class MyJoiner<T1, T2> : IInterface<T1>
		{
			public readonly IEnumerable<IInterface<JoinEvent<T1, T2>>> joiners;

			public MyJoiner(IEnumerable<IInterface<JoinEvent<T1, T2>>> joiners)
			{
				this.joiners = joiners;
			}
		}

		public class JoinHandler : IInterface<JoinEvent<int, string>>
		{
		}

		[Test]
		public void Test()
		{
			var handlers = container.GetAll<IInterface>().ToArray();
			Assert.That(handlers.Length, Is.EqualTo(2));
			Assert.That(handlers.OfType<MyJoiner<int, string>>().Single().joiners.Single(), Is.SameAs(handlers.OfType<JoinHandler>().Single()));
		}
	}
}