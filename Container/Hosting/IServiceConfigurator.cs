namespace SimpleContainer.Hosting
{
	public interface IServiceConfigurator<in TSettings, TService>
	{
		void Configure(TSettings settings, ServiceConfigurationBuilder<TService> builder);
	}

	public interface IServiceConfigurator<TService>
	{
		void Configure(ServiceConfigurationBuilder<TService> builder);
	}
}