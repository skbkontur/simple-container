using SimpleContainer.Infection;

namespace SimpleContainer.Configuration
{
	public class ServiceContractConfigurationBuilder<T> :
		AbstractServiceConfigurationBuilder<ServiceContractConfigurationBuilder<T>, ContractConfigurationBuilder, T>
	{
		public ServiceContractConfigurationBuilder(ContractConfigurationBuilder builder) : base(builder)
		{
		}

		public ServiceContractConfigurationBuilder<T> Contract<TContract>() where TContract : RequireContractAttribute, new()
		{
			return new ServiceContractConfigurationBuilder<T>(builder.Contract<TContract>());
		}

		public ServiceContractConfigurationBuilder<T> Contract(string contractName)
		{
			return new ServiceContractConfigurationBuilder<T>(builder.Contract(contractName));
		}
	}
}