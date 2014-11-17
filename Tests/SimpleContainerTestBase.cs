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

		protected IContainer Container(params Action<ContainerConfigurationBuilder>[] configureActions)
		{
			var factory = new HostingEnvironmentFactory(x => x.Name.StartsWith("SimpleContainer"));
			var targetTypes = GetType().GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public);
			var hostingEnvironment = factory.FromTypes(targetTypes);
			IContainer container;
			Action<ContainerConfigurationBuilder> configureAction = delegate(ContainerConfigurationBuilder builder)
			{
				foreach (var action in configureActions)
					action(builder);
			};
			var disposable = hostingEnvironment
				.CreateHost(Assembly.GetExecutingAssembly(), configureAction)
				.StartHosting(out container);
			disposables.Add(disposable);
			return container;
		}
	}
}