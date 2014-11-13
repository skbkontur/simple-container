using System;
using System.Reflection;

namespace SimpleContainer.Hosting
{
	public class ContainerHost : IServiceHost
	{
		private readonly IInheritanceHierarchy hierarchy;
		private readonly IContainerConfiguration configuration;
		private readonly Assembly primaryAssembly;

		public ContainerHost(IInheritanceHierarchy hierarchy, IContainerConfiguration configuration, Assembly primaryAssembly)
		{
			this.hierarchy = hierarchy;
			this.configuration = configuration;
			this.primaryAssembly = primaryAssembly;
		}

		public IDisposable StartHosting<T>(out T service)
		{
			var restrictedHierarchy = new RestrictedInheritanceHierarchy(primaryAssembly, hierarchy);
			var containerComponent = new ContainerComponent<T>(new SimpleContainer(configuration, restrictedHierarchy));
			service = containerComponent.CreateEntryPoint();
			return containerComponent;
		}
	}
}