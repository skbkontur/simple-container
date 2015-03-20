using System;
using System.Collections.Generic;

namespace SimpleContainer.Configuration
{
	internal class ContractConfiguration : ConfigurationRegistry
	{
		public string Name { get; private set; }
		public List<string> RequiredContracts { get; private set; }

		public ContractConfiguration(string name, List<string> requiredContracts,
			IDictionary<Type, object> configurations, List<string> unionContractNames)
			: base(configurations)
		{
			Name = name;
			RequiredContracts = requiredContracts;
			UnionContractNames = unionContractNames == null ? null : unionContractNames.ToArray();
		}

		public string[] UnionContractNames { get; private set; }
	}
}