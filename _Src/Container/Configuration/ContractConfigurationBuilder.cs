using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;

namespace SimpleContainer.Configuration
{
	public class ContractConfigurationBuilder : AbstractConfigurationBuilder<ContractConfigurationBuilder>
	{
		private readonly ContainerConfigurationBuilder containerConfigurationBuilder;
		private IEnumerable<string> unionContractNames;
		public List<string> RequiredContracts { get; private set; }
		public string Name { get; private set; }

		public ContractConfigurationBuilder(ContainerConfigurationBuilder containerConfigurationBuilder,
			string name, List<string> requiredContracts,
			ISet<Type> staticServices, bool isStaticConfiguration)
			: base(staticServices, isStaticConfiguration)
		{
			this.containerConfigurationBuilder = containerConfigurationBuilder;
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

		public ContractConfigurationBuilder Contract(string name)
		{
			return containerConfigurationBuilder.Contract(Enumerable.Concat(RequiredContracts, new[] {Name, name}).ToArray());
		}

		public ContractConfigurationBuilder Contract<T>()
			where T : RequireContractAttribute, new()
		{
			return Contract(InternalHelpers.NameOf<T>());
		}

		internal ContractConfiguration Build()
		{
			return new ContractConfiguration(Name, RequiredContracts, configurations,
				unionContractNames == null ? null : unionContractNames.ToList());
		}
	}
}