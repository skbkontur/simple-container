using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using SimpleContainer.Configuration;
using SimpleContainer.Hosting;

namespace SimpleContainer.Tests
{
	public abstract class SimpleContainerTestBase : UnitTestBase
	{
		private List<IDisposable> disposables;

		protected override void SetUp()
		{
			base.SetUp();
			disposables = new List<IDisposable>();
			LogBuilder = new StringBuilder();
		}

		public static StringBuilder LogBuilder { get; private set; }

		protected override void TearDown()
		{
			foreach (var disposable in disposables)
				disposable.Dispose();
			base.TearDown();
		}

		protected IStaticContainer CreateStaticContainer()
		{
			var targetTypes = GetType().GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public);
			var factory = new ContainerFactory(x => x.Name.StartsWith("SimpleContainer"));
			return factory.FromTypes(targetTypes);
		}

		protected IContainer Container(Action<ContainerConfigurationBuilder> configure = null)
		{
			var staticContainer = CreateStaticContainer();
			disposables.Add(staticContainer);
			var result = staticContainer.CreateLocalContainer(Assembly.GetExecutingAssembly(), configure);
			disposables.Add(result);
			return result;
		}
	}
}