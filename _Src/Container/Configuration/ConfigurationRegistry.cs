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

		private ConfigurationRegistry(IDictionary<Type, IServiceConfigurationSet> configurations,
			IDictionary<string, List<string>> contractUnions)
		{
			this.configurations = configurations;
			this.contractUnions = contractUnions;
		}

		public IServiceConfigurationSet GetConfiguration(Type type)
		{
			return configurations.GetOrDefault(type);
		}

		public List<string> GetContractsUnionOrNull(string contract)
		{
			return contractUnions.GetOrDefault(contract);
		}

		internal class Builder
		{
			private readonly IDictionary<Type, ServiceConfigurationSet> configurations =
				new Dictionary<Type, ServiceConfigurationSet>();

			private readonly IDictionary<string, List<string>> contractUnions = new Dictionary<string, List<string>>();

			public ServiceConfigurationSet GetConfigurationSet(Type type)
			{
				ServiceConfigurationSet result;
				if (!configurations.TryGetValue(type, out result))
					configurations.Add(type, result = new ServiceConfigurationSet());
				return result;
			}

			public void DefineContractsUnion(string contract, IEnumerable<string> contractNames, bool clearOld = false)
			{
				List<string> union;
				if (!contractUnions.TryGetValue(contract, out union))
					contractUnions.Add(contract, union = new List<string>());
				if (clearOld)
					union.Clear();
				union.AddRange(contractNames);
			}

			public ConfigurationRegistry Build()
			{
				var builtConfigurations = configurations.ToDictionary(x => x.Key, x => (IServiceConfigurationSet) x.Value);
				return new ConfigurationRegistry(builtConfigurations, contractUnions);
			}
		}
	}
}