using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;

namespace SimpleContainer.Configuration
{
	internal class ConfigurationRegistry : IConfigurationRegistry
	{
		private readonly IDictionary<Type, IServiceConfigurationSet> configurations;
		private readonly IDictionary<string, List<string>> contractUnions;
		private readonly ImplementationSelector[] implementationSelectors;

		private ConfigurationRegistry(IDictionary<Type, IServiceConfigurationSet> configurations,
			IDictionary<string, List<string>> contractUnions,
			ImplementationSelector[] implementationSelectors)
		{
			this.configurations = configurations;
			this.contractUnions = contractUnions;
			this.implementationSelectors = implementationSelectors;
		}

		public ServiceConfiguration GetConfigurationOrNull(Type type, List<string> contracts)
		{
			var configurationSet = configurations.GetOrDefault(type);
			return configurationSet == null ? null : configurationSet.GetConfiguration(contracts);
		}

		public List<string> GetContractsUnionOrNull(string contract)
		{
			return contractUnions.GetOrDefault(contract);
		}

		public ImplementationSelector[] GetImplementationSelectors()
		{
			return implementationSelectors;
		}

		internal class Builder
		{
			private readonly IDictionary<Type, ServiceConfigurationSet> configurations =
				new Dictionary<Type, ServiceConfigurationSet>();

			private readonly IDictionary<string, List<string>> contractUnions = new Dictionary<string, List<string>>();

			private readonly List<ImplementationSelector> implementationSelectors =
				new List<ImplementationSelector>();

			public ServiceConfigurationSet GetConfigurationSet(Type type)
			{
				ServiceConfigurationSet result;
				if (!configurations.TryGetValue(type, out result))
					configurations.Add(type, result = new ServiceConfigurationSet());
				return result;
			}

			public void DefineContractsUnion(string contract, List<string> contractNames, bool clearOld = false)
			{
				List<string> union;
				if (!contractUnions.TryGetValue(contract, out union))
					contractUnions.Add(contract, union = new List<string>());
				if (clearOld)
					union.Clear();
				union.AddRange(contractNames);
			}

			public void RegisterImplementationSelector(ImplementationSelector s)
			{
				implementationSelectors.Add(s);
			}

			public ConfigurationRegistry Build()
			{
				var builtConfigurations = configurations.ToDictionary(x => x.Key, x => (IServiceConfigurationSet) x.Value);
				return new ConfigurationRegistry(builtConfigurations, contractUnions, implementationSelectors.ToArray());
			}
		}
	}
}