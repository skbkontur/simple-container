using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;
using SimpleContainer.Interface;

namespace SimpleContainer.Configuration
{
	public class ContainerConfigurationBuilder : AbstractConfigurationBuilder<ContainerConfigurationBuilder>
	{
		public ContainerConfigurationBuilder(ISet<Type> staticServices, bool isStatic)
			: base(new ConfigurationRegistry.Builder(), new List<string>(), staticServices, isStatic)
		{
		}

		public ContainerConfigurationBuilder MakeStatic(Type type)
		{
			if (!isStatic)
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
			return Contract(InternalHelpers.NameOf<T>());
		}

		public ContractConfigurationBuilder Contract(params string[] newContracts)
		{
			return new ContractConfigurationBuilder(RegistryBuilder, contracts.Concat(newContracts.ToList()), staticServices, isStatic);
		}
	}
}