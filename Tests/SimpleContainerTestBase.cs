using System;
using System.Collections.Generic;
using System.Reflection;
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
		}

		protected override void TearDown()
		{
			foreach (var disposable in disposables)
				disposable.Dispose();
			base.TearDown();
		}

		protected IDisposable StartHosting<T>(Action<ContainerConfigurationBuilder> configureAction, out T service)
		{
			var factory = new HostingEnvironmentFactory(x => x.Name.StartsWith("SimpleContainer"));
			var targetTypes = GetType().GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public);
			var hostingEnvironment = factory.FromTypes(targetTypes);
			return hostingEnvironment
				.CreateHost(Assembly.GetExecutingAssembly(), configureAction)
				.StartHosting(out service);
		}

		protected IContainer Container(Action<ContainerConfigurationBuilder> configureActions = null)
		{
			IContainer result;
			disposables.Add(StartHosting(configureActions, out result));
			return result;
		}
	}
}