namespace SimpleContainer.Configuration
{
	public class ServiceProfileConfigurationBuilder<T> :
		AbstractServiceConfigurationBuilder<ServiceProfileConfigurationBuilder<T>, ProfileConfigurationBuilder, T>
	{
		public ServiceProfileConfigurationBuilder(ProfileConfigurationBuilder builder) : base(builder)
		{
		}
	}
}