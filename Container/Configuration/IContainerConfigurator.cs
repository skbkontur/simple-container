namespace SimpleContainer.Configuration
{
	public interface IContainerConfigurator
	{
		void Configure(ContainerConfigurationBuilder builder);
	}

	public interface IContainerConfigurator<in TSettings>
	{
		void Configure(TSettings settings, ContainerConfigurationBuilder builder);
	}
}