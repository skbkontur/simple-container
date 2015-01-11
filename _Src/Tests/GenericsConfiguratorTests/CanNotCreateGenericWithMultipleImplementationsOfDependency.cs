using NUnit.Framework;
using SimpleContainer.Interface;

namespace SimpleContainer.Tests.GenericsConfiguratorTests
{
	public class CanNotCreateGenericWithMultipleImplementationsOfDependency : SimpleContainerTestBase
	{
		public interface IService
		{
		}

		public class Service<TEvent> : IService
		{
			private readonly IDependency<TEvent> dependency;

			public Service(IDependency<TEvent> dependency)
			{
				this.dependency = dependency;
			}
		}

		public interface IDependency<TEvent>
		{
		}

		public class Dependency<T> : IDependency<GenericParameter>
			where T : IConstraint
		{
		}

		public class GenericParameter
		{
		}

		public interface IConstraint
		{
		}

		public class Constraint1 : IConstraint
		{
		}

		public class Constraint2 : IConstraint
		{
		}

		[Test]
		public void Test()
		{
			var container = Container();
			Assert.Throws<SimpleContainerException>(() => container.Get<IService>());
		}
	}
}