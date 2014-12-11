using System;
using System.Collections.Generic;

namespace SimpleContainer.Configuration
{
	internal class ContractConfiguration : ConfigurationRegistry
	{
		public ContractConfiguration(IDictionary<Type, object> configurations, List<string> unionContractNames)
			: base(configurations)
		{
			UnionContractNames = unionContractNames;
		}

		public List<string> UnionContractNames { get; private set; }
	}
}