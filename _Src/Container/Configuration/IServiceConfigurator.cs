using JetBrains.Annotations;

namespace SimpleContainer.Configuration
{
	[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
	public interface IServiceConfigurator<TService>
	{
		void Configure(ConfigurationContext context, ServiceConfigurationBuilder<TService> builder);
	}
}