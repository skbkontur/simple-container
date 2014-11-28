using System;
using System.Collections.Generic;

namespace SimpleContainer.Configuration
{
	public class ContractConfiguration : ConfigurationRegistry
	{
		public ContractConfiguration(IDictionary<Type, object> configurations, IEnumerable<string> unionContractNames)
			: base(configurations)
		{
			UnionContractNames = unionContractNames;
		}

		public IEnumerable<string> UnionContractNames { get; private set; }
	}
}