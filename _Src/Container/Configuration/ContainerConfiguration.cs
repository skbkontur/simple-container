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

		public IEnumerable<ContractConfiguration> GetContractConfigurations(string contract)
		{
			return contractsConfigurators.Where(x => x.Name == contract).OrderBy(x => x.RequiredContracts.Count);
		}
	}
}