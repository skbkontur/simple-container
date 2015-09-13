using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;
using SimpleContainer.Interface;

namespace SimpleContainer.Configuration
{
	public class ContractConfigurationBuilder : AbstractConfigurationBuilder<ContractConfigurationBuilder>
	{
		internal ContractConfigurationBuilder(ConfigurationRegistry.Builder registryBuilder, List<string> contracts)
			: base(registryBuilder, contracts)
		{
		}

		public ContractConfigurationBuilder UnionOf(IEnumerable<string> contractNames)
		{
			if (contracts.Count != 1)
			{
				const string messageFormat = "UnionOf can be applied to single contract, current contracts [{0}]";
				throw new SimpleContainerException(string.Format(messageFormat, contracts.JoinStrings(", ")));
			}
			RegistryBuilder.DefineContractsUnion(contracts[0], contractNames.ToList());
			return this;
		}

		public ContractConfigurationBuilder Union<TContract>()
			where TContract : RequireContractAttribute, new()
		{
			return UnionOf(InternalHelpers.NameOf<TContract>());
		}

		public ContractConfigurationBuilder UnionOf(params string[] contractNames)
		{
			return UnionOf(contractNames.AsEnumerable());
		}
	}
}