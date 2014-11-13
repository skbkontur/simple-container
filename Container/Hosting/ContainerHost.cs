using System;

namespace SimpleContainer.Hosting
{
	public class ContainerHost : IServiceHost
	{
		private readonly IInheritanceHierarchy hierarchy;
		private readonly IContainerConfiguration configuration;

		public ContainerHost(IInheritanceHierarchy hierarchy, IContainerConfiguration configuration)
		{
			this.hierarchy = hierarchy;
			this.configuration = configuration;
		}

		public IDisposable StartHosting<T>(out T service)
		{
			var containerComponent = new ContainerComponent<T>(new SimpleContainer(configuration, hierarchy));
			service = containerComponent.CreateEntryPoint();
			return containerComponent;
		}
	}
}