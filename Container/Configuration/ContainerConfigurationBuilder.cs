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
		private readonly IDictionary<string, ContractConfigurationBuilder> contractConfigurators =
			new Dictionary<string, ContractConfigurationBuilder>();

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

		public ContractConfigurationBuilder Contract(string contract)
		{
			ContractConfigurationBuilder result;
			if (!contractConfigurators.TryGetValue(contract, out result))
				contractConfigurators.Add(contract, result = new ContractConfigurationBuilder(staticServices, isStaticConfiguration));
			return result;
		}

		internal IContainerConfiguration Build()
		{
			return new ContainerConfiguration(configurations,
				contractConfigurators.ToDictionary(x => x.Key, x => x.Value.Build()));
		}
	}
}