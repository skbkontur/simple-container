using System;

namespace SimpleContainer.Hosting
{
	public class ContainerHost
	{
		private readonly IInheritanceHierarchy hierarchy;
		private readonly IContainerConfiguration configuration;
		private readonly Action<ContainerConfigurationBuilder> configure;
		private readonly IShutdownCoordinator shutdownCoordinator;

		public ContainerHost(IInheritanceHierarchy hierarchy, IContainerConfiguration configuration,
			Action<ContainerConfigurationBuilder> configure, IShutdownCoordinator shutdownCoordinator)
		{
			this.hierarchy = hierarchy;
			this.configuration = configuration;
			this.configure = configure;
			this.shutdownCoordinator = shutdownCoordinator;
		}

		public IDisposable StartHosting<T>(out T service)
		{
			return StartHosting(null, out service);
		}

		public IDisposable StartHosting<T>(string contract, out T service)
		{
			return StartHosting(new ServiceName {type = typeof (T), contract = contract}, out service);
		}

		public IDisposable StartHosting<T>(ServiceName name, out T service)
		{
			var configurationBuilder = new ContainerConfigurationBuilder();
			configurationBuilder.Bind<IShutdownCoordinator>(shutdownCoordinator);
			configurationBuilder.Bind<IServiceHost>(new ServiceHost(this, name.contract));
			if (configure != null)
				configure(configurationBuilder);
			var containerConfiguration = new MergedConfiguration(configuration, configurationBuilder.Build());
			var containerComponent = new ContainerComponent(new SimpleContainer(containerConfiguration, hierarchy), name);
			service = (T) containerComponent.CreateEntryPoint();
			return containerComponent;
		}

		private class ServiceHost : IServiceHost
		{
			private readonly ContainerHost host;
			private readonly string contract;

			public ServiceHost(ContainerHost host, string contract)
			{
				this.host = host;
				this.contract = contract;
			}

			public IDisposable StartHosting<T>(out T service)
			{
				return host.StartHosting(contract, out service);
			}
		}
	}
}