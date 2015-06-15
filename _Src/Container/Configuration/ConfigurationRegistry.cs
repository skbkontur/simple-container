using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Configuration
{
	internal class ConfigurationRegistry : IConfigurationRegistry
	{
		private readonly IDictionary<Type, IServiceConfigurationSet> configurations;
		private readonly IDictionary<string, List<string>> contractUnions;
		private readonly IDictionary<Type, Type[]> genericMappings;
		private readonly ImplementationFilter[] implementationFilters;

		private ConfigurationRegistry(IDictionary<Type, IServiceConfigurationSet> configurations,
			IDictionary<string, List<string>> contractUnions,
			IDictionary<Type, Type[]> genericMappings,
			ImplementationFilter[] implementationFilters)
		{
			this.configurations = configurations;
			this.contractUnions = contractUnions;
			this.genericMappings = genericMappings;
			this.implementationFilters = implementationFilters;
		}

		public Type[] GetGenericMappingsOrNull(Type type)
		{
			return genericMappings.GetOrDefault(type);
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

		public ImplementationFilter[] GetImplementationFilters()
		{
			return implementationFilters;
		}

		internal class Builder
		{
			private readonly IDictionary<Type, ServiceConfigurationSet> configurations =
				new Dictionary<Type, ServiceConfigurationSet>();

			private readonly IDictionary<string, List<string>> contractUnions = new Dictionary<string, List<string>>();
			private readonly IDictionary<Type, List<Type>> genericMappings = new Dictionary<Type, List<Type>>();

			private readonly IDictionary<string, ImplementationFilter> implementationFilters =
				new Dictionary<string, ImplementationFilter>();

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

			public void DefinedGenericMapping(Type from, Type to)
			{
				List<Type> mappings;
				if (!genericMappings.TryGetValue(from, out mappings))
					genericMappings.Add(from, mappings = new List<Type>());
				mappings.Add(to);
			}

			public void RegisterImplementationFilter(string name, Func<Type, Type, bool> f)
			{
				if (implementationFilters.ContainsKey(name))
					throw new SimpleContainerException(string.Format("impementation filter [{0}] already registered", name));
				var result = new ImplementationFilter(name, f);
				implementationFilters.Add(name, result);
			}

			public ConfigurationRegistry Build()
			{
				var builtConfigurations = configurations.ToDictionary(x => x.Key, x => (IServiceConfigurationSet) x.Value);
				var builtMappings = genericMappings.ToDictionary(x => x.Key, x => x.Value.ToArray());
				return new ConfigurationRegistry(builtConfigurations, contractUnions,
					builtMappings, implementationFilters.Values.ToArray());
			}
		}
	}
}