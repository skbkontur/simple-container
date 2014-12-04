namespace SimpleContainer.Configuration
{
	public interface IServiceConfigurator<TService>
	{
		void Configure(ConfigurationContext context, ServiceConfigurationBuilder<TService> builder);
	}
}