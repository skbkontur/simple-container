using System;
using System.Collections.Generic;
using SimpleContainer.Helpers;

namespace SimpleContainer.Configuration
{
	internal class ContainerConfiguration : ConfigurationRegistry, IContainerConfiguration
	{
		private readonly IDictionary<string, ContractConfiguration> contractsConfigurators;

		public ContainerConfiguration(IDictionary<Type, object> configurations,
			IDictionary<string, ContractConfiguration> contractsConfigurators)
			: base(configurations)
		{
			this.contractsConfigurators = contractsConfigurators;
		}

		public ContractConfiguration GetContractConfiguration(string contract)
		{
			return contractsConfigurators.GetOrDefault(contract);
		}
	}
}