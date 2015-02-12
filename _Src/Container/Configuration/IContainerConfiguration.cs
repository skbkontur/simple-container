namespace SimpleContainer.Configuration
{
	internal interface IContainerConfiguration : IContainerConfigurationRegistry
	{
		ContractConfiguration[] GetContractConfigurations(string contract);
	}
}