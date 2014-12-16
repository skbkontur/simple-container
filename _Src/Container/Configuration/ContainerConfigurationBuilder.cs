using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation;
using SimpleContainer.Infection;

namespace SimpleContainer.Configuration
{
	public class ContainerConfigurationBuilder : AbstractConfigurationBuilder<ContainerConfigurationBuilder>
	{
		private readonly List<ContractConfigurationBuilder> contractConfigurators =
			new List<ContractConfigurationBuilder>();

		public ContainerConfigurationBuilder(ISet<Type> staticServices, bool isStaticConfiguration)
			: base(staticServices, isStaticConfiguration)
		{
		}

		public ContainerConfigurationBuilder MakeStatic(Type type)
		{
			if (!isStaticConfiguration)
			{
				const string messageFormat = "can't make type [{0}] static using non static configurator";
				throw new SimpleContainerException(string.Format(messageFormat, type.FormatName()));
			}
			staticServices.Add(type);
			return this;
		}

		public ContractConfigurationBuilder Contract<T>()
			where T : RequireContractAttribute, new()
		{
			return Contract(new T().ContractName);
		}

		public ContractConfigurationBuilder Contract(params string[] contracts)
		{
			if (contracts.Length == 0)
				throw new InvalidOperationException("contracts is empty");
			var contractName = contracts[contracts.Length - 1];
			var requiredContracts = contracts.Length == 1
				? new List<string>(0)
				: contracts.Where((_, i) => i < contracts.Length - 1).ToList();
			var requiredContractsKey = InternalHelpers.FormatContractsKey(requiredContracts);
			var result = contractConfigurators
				.Where(x => x.Name == contractName)
				.SingleOrDefault(x => InternalHelpers.FormatContractsKey(x.RequiredContracts) == requiredContractsKey);
			if (result == null)
			{
				result = new ContractConfigurationBuilder(contractName, requiredContracts, staticServices, isStaticConfiguration);
				contractConfigurators.Add(result);
			}
			return result;
		}

		internal IContainerConfiguration Build()
		{
			return new ContainerConfiguration(configurations, contractConfigurators.Select(x => x.Build()).ToArray());
		}
	}
}