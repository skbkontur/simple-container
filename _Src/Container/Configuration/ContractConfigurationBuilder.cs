using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleContainer.Configuration
{
	public class ContractConfigurationBuilder : AbstractConfigurationBuilder<ContractConfigurationBuilder>
	{
		private IEnumerable<string> unionContractNames;
		public List<string> RequiredContracts { get; private set; }
		public string Name { get; private set; }

		public ContractConfigurationBuilder(string name, List<string> requiredContracts,
			ISet<Type> staticServices, bool isStaticConfiguration)
			: base(staticServices, isStaticConfiguration)
		{
			Name = name;
			RequiredContracts = requiredContracts;
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
			return new ContractConfiguration(Name, RequiredContracts, configurations,
				unionContractNames == null ? null : unionContractNames.ToList());
		}
	}
}