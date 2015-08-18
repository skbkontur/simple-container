using System.Linq;
using NUnit.Framework;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.Generics
{
	public class CanCloseSingleGenericInterfaceWithMultipleParameters : SimpleContainerTestBase
	{
		public interface IMarkerInterface
		{
		}

		public interface IGenericInterface<T> : IMarkerInterface
		{
		}

		public class GenericImplementation<T> : IGenericInterface<T>
		{
			public IDependency<T> dependency;

			public GenericImplementation(IDependency<T> dependency)
			{
				this.dependency = dependency;
			}
		}

		public interface IDependency<T>
		{
		}

		public class ClosingDependency : IDependency<double>, IDependency<int>
		{
		}

		[Test]
		public void Test()
		{
			var container = Container();
			var implementations = container.GetAll<IMarkerInterface>();
			Assert.That(implementations.Select(x => x.GetType().GetGenericArguments().Single()),
				Is.EquivalentTo(new[] {typeof (int), typeof (double)}));
		}
	}
}