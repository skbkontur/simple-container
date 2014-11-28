using System.Collections.Generic;
using System.Linq;

namespace SimpleContainer.Configuration
{
	public class ContractConfigurationBuilder : ContainerConfigurationBuilder
	{
		private IEnumerable<string> unionContractNames;

		public ContractConfigurationBuilder Union(IEnumerable<string> contractNames)
		{
			unionContractNames = contractNames;
			return this;
		}

		public ContractConfigurationBuilder Union(params string[] contractNames)
		{
			return Union(contractNames.AsEnumerable());
		}

		public new ContractConfiguration Build()
		{
			return new ContractConfiguration(configurations, unionContractNames);
		}
	}
}