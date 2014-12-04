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

		private readonly IDictionary<string, ProfileConfigurationBuilder> profileConfigurators =
			new Dictionary<string, ProfileConfigurationBuilder>();

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

		public ProfileConfigurationBuilder Profile<TProfile>()
		{
			return Profile(typeof (TProfile).Name);
		}

		public ProfileConfigurationBuilder Profile(string name)
		{
			ProfileConfigurationBuilder result;
			if (!profileConfigurators.TryGetValue(name, out result))
				profileConfigurators.Add(name, result = new ProfileConfigurationBuilder(staticServices, isStaticConfiguration));
			return result;
		}

		internal IContainerConfiguration Build(string profile)
		{
			IContainerConfiguration result = new ContainerConfiguration(configurations,
				contractConfigurators.ToDictionary(x => x.Key, x => x.Value.Build()));
			ProfileConfigurationBuilder profileBuilder;
			return profile != null && profileConfigurators.TryGetValue(profile, out profileBuilder)
				? new MergedConfiguration(result, profileBuilder.Build())
				: result;
		}
	}
}