using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;

namespace SimpleContainer.Configuration
{
	public class ContractConfigurationBuilder : AbstractConfigurationBuilder<ContractConfigurationBuilder>
	{
		internal ContractConfigurationBuilder(ConfigurationRegistry.Builder registryBuilder, List<string> contracts,
			ISet<Type> staticServices, bool isStaticConfiguration)
			: base(registryBuilder, contracts, staticServices, isStaticConfiguration)
		{
		}

		public ContractConfigurationBuilder UnionOf(IEnumerable<string> contractNames, bool clearOld = false)
		{
			if (contracts.Count != 1)
				throw new InvalidOperationException("assertion failure");
			RegistryBuilder.DefineContractsUnion(contracts[0], contractNames, clearOld);
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
			return new ContractConfigurationBuilder(RegistryBuilder, contracts.Concat(newContracts.ToList()),
				staticServices, isStatic);
		}

		public ContractConfigurationBuilder Contract<T>()
			where T : RequireContractAttribute, new()
		{
			return Contract(InternalHelpers.NameOf<T>());
		}
	}
}