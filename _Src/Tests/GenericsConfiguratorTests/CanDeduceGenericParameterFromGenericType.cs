using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.GenericsConfiguratorTests
{
	public class CanDeduceGenericParameterFromGenericType : SimpleContainerTestBase
	{
		public interface IParameter
		{
		}

		public interface IParameter<T> : IParameter
		{
		}

		public class JoinEvent<T1, T2>
		{
		}

		public interface IInterface
		{
		}

		public class IInterface<T> : IInterface
		{
			public readonly IEnumerable<IParameter<T>> handleres;

			public IInterface(IEnumerable<IParameter<T>> handleres)
			{
				this.handleres = handleres;
			}
		}

		public class MyJoiner<T1, T2> : IParameter<T1>
		{
			public readonly IEnumerable<IParameter<JoinEvent<T1, T2>>> joiners;

			public MyJoiner(IEnumerable<IParameter<JoinEvent<T1, T2>>> joiners)
			{
				this.joiners = joiners;
			}
		}

		public class JoinHandler : IParameter<JoinEvent<int, string>>
		{
		}

		[Test]
		public void Test()
		{
			var handlers = Container().GetAll<IInterface>().ToArray();
			Assert.That(handlers.Length, Is.EqualTo(2));
			Assert.That(handlers.OfType<IInterface<int>>().Single().handleres.Single(), Is.InstanceOf<MyJoiner<int, string>>());
			Assert.That(handlers.OfType<IInterface<JoinEvent<int, string>>>().Single().handleres.Single(),
				Is.InstanceOf<JoinHandler>());
		}
	}
}