using System.Linq;
using NUnit.Framework;

namespace SimpleContainer.Tests.GenericsConfiguratorTests
{
	public class CanHaveMultipleGenericInterfaces : GenericConfigurationTestBase
	{
		public interface IGenericInterface1<T>
		{
		}

		public interface IGenericInterface2<T>
		{
		}

		public interface IComponentInterface
		{
		}

		public class GenericComponent<T> : IGenericInterface1<T>, IGenericInterface2<T>, IComponentInterface
		{
			public readonly IDependency<T> dependency;

			public GenericComponent(IDependency<T> dependency)
			{
				this.dependency = dependency;
			}
		}

		public interface IDependency<T>
		{
		}

		public class ClosedDependency : IDependency<int>
		{
		}

		[Test]
		public void Test()
		{
			var components = container.GetAll<IComponentInterface>().ToArray();
			Assert.That(components.Length, Is.EqualTo(1));
			Assert.That(components[0], Is.SameAs(container.Get<IGenericInterface1<int>>()));
			Assert.That(components[0], Is.SameAs(container.Get<IGenericInterface2<int>>()));
		}
	}
}