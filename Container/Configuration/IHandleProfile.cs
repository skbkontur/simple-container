using System.Collections.Specialized;

namespace SimpleContainer.Configuration
{
	public interface IHandleProfile
	{
	}

	public interface IHandleProfile<TProfile> : IHandleProfile
		where TProfile : IProfile
	{
		void Handle(NameValueCollection applicationSettings, ContainerConfigurationBuilder builder);
	}

	public interface IConfigurator
	{
		void Configure(ContainerConfigurationBuilder builder);
	}

	public interface IConfigurator<in TSettings>
	{
		void Configure(TSettings settings, ContainerConfigurationBuilder builder);
	}

	public interface IServiceConfigurator<TService>
	{
		void Configure(ServiceConfigurationBuilder<TService> builder);
	}

	public interface IServiceConfigurator<in TSettings, TService>
	{
		void Configure(TSettings settings, ServiceConfigurationBuilder<TService> builder);
	}
}