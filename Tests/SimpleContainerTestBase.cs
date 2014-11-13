using System;
using System.Reflection;
using SimpleContainer.Hosting;

namespace SimpleContainer.Tests
{
	public abstract class SimpleContainerTestBase : UnitTestBase
	{
		protected SimpleContainer Container(params Action<ContainerConfigurationBuilder>[] configure)
		{
			var factory = new HostingEnvironmentFactory(x => x.Name.StartsWith("SimpleContainer"));
			var targetTypes = GetType().GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public);
			var hostingEnvironment = factory.Create(targetTypes);
			return hostingEnvironment.CreateContainer(delegate(ContainerConfigurationBuilder builder)
			{
				foreach (var action in configure)
					action(builder);
			});
		}
	}
}