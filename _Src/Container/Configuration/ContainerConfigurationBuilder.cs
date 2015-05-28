using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;

namespace SimpleContainer.Configuration
{
	public class ContainerConfigurationBuilder : AbstractConfigurationBuilder<ContainerConfigurationBuilder>
	{
		public StaticServicesConfigurator StaticServices { get; private set; }

		public ContainerConfigurationBuilder(bool isStatic)
			: base(new ConfigurationRegistry.Builder(), new List<string>())
		{
			StaticServices = new StaticServicesConfigurator(isStatic);
		}

		public ContainerConfigurationBuilder MakeStatic(Type type)
		{
			StaticServices.MakeStatic(type);
			return this;
		}

		public ContractConfigurationBuilder Contract<T>()
			where T : RequireContractAttribute, new()
		{
			return Contract(InternalHelpers.NameOf<T>());
		}

		public ContractConfigurationBuilder Contract(params string[] newContracts)
		{
			return new ContractConfigurationBuilder(RegistryBuilder, contracts.Concat(newContracts.ToList()));
		}
	}
}