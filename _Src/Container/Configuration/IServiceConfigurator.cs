namespace SimpleContainer.Configuration
{
	public interface IServiceConfigurator<TService>
	{
		void Configure(ConfigurationContext context, ServiceConfigurationBuilder<TService> builder);
	}

	public interface IMediumPriorityServiceConfigurator<TService> : IServiceConfigurator<TService>
	{
	}

	public interface IHighPriorityServiceConfigurator<TService> : IServiceConfigurator<TService>
	{
	}
}