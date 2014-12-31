using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleContainer.Configuration
{
	internal class ContainerConfiguration : ConfigurationRegistry, IContainerConfiguration
	{
		private readonly IEnumerable<ContractConfiguration> contractsConfigurators;

		public ContainerConfiguration(IDictionary<Type, object> configurations,
			IEnumerable<ContractConfiguration> contractsConfigurators)
			: base(configurations)
		{
			this.contractsConfigurators = contractsConfigurators;
		}

		public ContractConfiguration[] GetContractConfigurations(string contract)
		{
			return contractsConfigurators.Where(x => x.Name == contract).ToArray();
		}
	}
}