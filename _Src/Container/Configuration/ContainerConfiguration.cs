using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;

namespace SimpleContainer.Configuration
{
	internal class ContainerConfiguration : ConfigurationRegistry, IContainerConfiguration
	{
		private readonly IDictionary<string, ContractConfiguration[]> contractsConfigurators;

		public ContainerConfiguration(IDictionary<Type, object> configurations,
			IEnumerable<ContractConfiguration> contractsConfigurators)
			: base(configurations)
		{
			this.contractsConfigurators = contractsConfigurators
				.GroupBy(x => x.Name)
				.ToDictionary(x => x.Key, x => x.OrderBy(c => c.RequiredContracts.Count).ToArray());
		}

		public ContractConfiguration[] GetContractConfigurations(string contract)
		{
			return contractsConfigurators.GetOrDefault(contract);
		}
	}
}