using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation;

namespace SimpleContainer.Configuration
{
	internal class ConfigurationRegistry
	{
		private readonly IConfigurationSource configurations;
		private readonly IDictionary<string, List<string>> contractUnions;
		private readonly List<ImplementationSelector> implementationSelectors;
		public static ConfigurationRegistry Empty { get; private set; }

		static ConfigurationRegistry()
		{
			Empty = new ConfigurationRegistry(new SimpleConfigurationSource(new Dictionary<Type, ServiceConfigurationSet>()),
				new Dictionary<string, List<string>>(), new List<ImplementationSelector>());
		}

		private ConfigurationRegistry(IConfigurationSource configurations,
			IDictionary<string, List<string>> contractUnions,
			List<ImplementationSelector> implementationSelectors)
		{
			this.configurations = configurations;
			this.contractUnions = contractUnions;
			this.implementationSelectors = implementationSelectors;
		}

		public ServiceConfiguration GetConfigurationOrNull(Type type, List<string> contracts)
		{
			var configurationSet = configurations.Get(type);
			return configurationSet == null ? null : configurationSet.GetConfiguration(contracts);
		}

		public List<string> GetContractsUnionOrNull(string contract)
		{
			return contractUnions.GetOrDefault(contract);
		}

		public List<ImplementationSelector> GetImplementationSelectors()
		{
			return implementationSelectors;
		}

		public ConfigurationRegistry Apply(TypesList typesList, Action<ContainerConfigurationBuilder> modificator)
		{
			if (modificator == null)
				return this;
			var builder = new ContainerConfigurationBuilder();
			modificator(builder);
			return builder.RegistryBuilder.Build(typesList, this);
		}

		internal class Builder
		{
			private readonly Dictionary<Type, ServiceConfigurationSet> configurations =
				new Dictionary<Type, ServiceConfigurationSet>();

			private readonly List<DynamicConfiguration> dynamicConfigurators = new List<DynamicConfiguration>();

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

			public void DefineContractsUnion(string contract, List<string> contractNames)
			{
				List<string> union;
				if (!contractUnions.TryGetValue(contract, out union))
					contractUnions.Add(contract, union = new List<string>());
				union.AddRange(contractNames);
			}

			public void RegisterImplementationSelector(ImplementationSelector s)
			{
				implementationSelectors.Add(s);
			}

			public void Filtered(string description, Type baseType,
				Action<Type, ServiceConfigurationBuilder<object>> configureAction)
			{
				dynamicConfigurators.Add(new DynamicConfiguration(description, baseType, configureAction));
			}

			public ConfigurationRegistry Build(TypesList typesList, ConfigurationRegistry parent)
			{
				ApplyDynamicConfigurators(typesList);
				IConfigurationSource configurationSource = new SimpleConfigurationSource(configurations);
				if (parent != null)
				{
					foreach (var p in parent.contractUnions)
						if (!contractUnions.ContainsKey(p.Key))
							contractUnions.Add(p);
					implementationSelectors.AddRange(parent.implementationSelectors);
					configurationSource = new MergingConfigurationSource(configurationSource, parent.configurations);
				}
				return new ConfigurationRegistry(configurationSource, contractUnions, implementationSelectors);
			}

			private void ApplyDynamicConfigurators(TypesList typesList)
			{
				var configurationSet = new ServiceConfigurationSet();
				var builder = new ServiceConfigurationBuilder<object>(configurationSet);
				foreach (var c in dynamicConfigurators)
				{
					var targetTypes = c.BaseType == null ? typesList.Types.ToList() : typesList.InheritorsOf(c.BaseType);
					foreach (var t in targetTypes)
					{
						if (configurations.ContainsKey(t))
							continue;
						c.ConfigureAction(t, builder);
						if (configurationSet.IsEmpty())
							continue;
						if (!string.IsNullOrEmpty(c.Description))
							configurationSet.SetDefaultComment(c.Description);
						configurations.Add(t, configurationSet);
						configurationSet = new ServiceConfigurationSet();
						builder = new ServiceConfigurationBuilder<object>(configurationSet);
					}
				}
			}
		}

		private interface IConfigurationSource
		{
			ServiceConfigurationSet Get(Type type);
		}

		private class SimpleConfigurationSource : IConfigurationSource
		{
			private readonly Dictionary<Type, ServiceConfigurationSet> impl;

			public SimpleConfigurationSource(Dictionary<Type, ServiceConfigurationSet> impl)
			{
				this.impl = impl;
			}

			public ServiceConfigurationSet Get(Type type)
			{
				return impl.GetOrDefault(type);
			}
		}

		private class MergingConfigurationSource : IConfigurationSource
		{
			private readonly IConfigurationSource child;
			private readonly IConfigurationSource parent;

			public MergingConfigurationSource(IConfigurationSource child, IConfigurationSource parent)
			{
				this.child = child;
				this.parent = parent;
			}

			public ServiceConfigurationSet Get(Type type)
			{
				var c = child.Get(type);
				var p = parent.Get(type);
				if (c == null)
					return p;
				if (p != null && c.parent == null)
					c.parent = p;
				return c;
			}
		}
	}
}