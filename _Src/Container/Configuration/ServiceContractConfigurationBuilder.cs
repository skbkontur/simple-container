namespace SimpleContainer.Configuration
{
	public class ServiceContractConfigurationBuilder<T> :
		AbstractServiceConfigurationBuilder<ServiceContractConfigurationBuilder<T>, ContractConfigurationBuilder, T>
	{
		public ServiceContractConfigurationBuilder(ContractConfigurationBuilder builder) : base(builder)
		{
		}
	}
}