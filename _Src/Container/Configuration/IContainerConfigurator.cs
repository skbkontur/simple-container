namespace SimpleContainer.Configuration
{
	public interface IContainerConfigurator
	{
		void Configure(ConfigurationContext context, ContainerConfigurationBuilder builder);
	}
}