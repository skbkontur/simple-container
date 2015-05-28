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

		public ContractConfigurationBuilder UnionOf(IEnumerable<string> contractNames, bool clearOld = false)
		{
			if (contracts.Count != 1)
			{
				const string messageFormat = "UnionOf can be applied to single contract, current contracts [{0}]";
				throw new SimpleContainerException(string.Format(messageFormat, contracts.JoinStrings(", ")));
			}
			RegistryBuilder.DefineContractsUnion(contracts[0], contractNames.ToList(), clearOld);
			return this;
		}

		public ContractConfigurationBuilder Union<TContract>(bool clearOld = false)
			where TContract : RequireContractAttribute, new()
		{
			return UnionOf(clearOld, InternalHelpers.NameOf<TContract>());
		}

		public ContractConfigurationBuilder UnionOf(params string[] contractNames)
		{
			return UnionOf(contractNames.AsEnumerable());
		}

		public ContractConfigurationBuilder UnionOf(bool clearOld, params string[] contractNames)
		{
			return UnionOf(contractNames.AsEnumerable(), clearOld);
		}

		public ContractConfigurationBuilder Contract(params string[] newContracts)
		{
			return new ContractConfigurationBuilder(RegistryBuilder, contracts.Concat(newContracts.ToList()));
		}

		public ContractConfigurationBuilder Contract<T>()
			where T : RequireContractAttribute, new()
		{
			return Contract(InternalHelpers.NameOf<T>());
		}
	}
}