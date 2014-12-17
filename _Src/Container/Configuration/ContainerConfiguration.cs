using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleContainer.Configuration
{
	internal class ContainerConfiguration : ConfigurationRegistry, IContainerConfiguration
	{
		private readonly IEnumerable<string> defaultContracts;
		private readonly IEnumerable<ContractConfiguration> contractsConfigurators;

		public ContainerConfiguration(IEnumerable<string> defaultContracts, IDictionary<Type, object> configurations,
			IEnumerable<ContractConfiguration> contractsConfigurators)
			: base(configurations)
		{
			this.defaultContracts = defaultContracts;
			this.contractsConfigurators = contractsConfigurators;
		}

		public IEnumerable<ContractConfiguration> GetContractConfigurations(string contract)
		{
			return contractsConfigurators.Where(x => x.Name == contract).OrderBy(x => x.RequiredContracts.Count);
		}

		public IEnumerable<string> DefaultContracts()
		{
			return defaultContracts;
		}
	}
}