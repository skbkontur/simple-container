using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleContainer.Configuration
{
	public class ContractConfigurationBuilder : AbstractConfigurationBuilder<ContractConfigurationBuilder>
	{
		private IEnumerable<string> unionContractNames;

		public ContractConfigurationBuilder(ISet<Type> staticServices, bool isStaticConfiguration)
			: base(staticServices, isStaticConfiguration)
		{
		}

		public ContractConfigurationBuilder UnionOf(IEnumerable<string> contractNames)
		{
			unionContractNames = contractNames;
			return this;
		}

		public ContractConfigurationBuilder UnionOf(params string[] contractNames)
		{
			return UnionOf(contractNames.AsEnumerable());
		}

		internal ContractConfiguration Build()
		{
			return new ContractConfiguration(configurations, unionContractNames == null ? null : unionContractNames.ToList());
		}
	}
}