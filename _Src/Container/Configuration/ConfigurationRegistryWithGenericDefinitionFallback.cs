using System;
using System.Collections.Generic;
using SimpleContainer.Helpers;

namespace SimpleContainer.Configuration
{
	internal class ConfigurationRegistryWithGenericDefinitionFallback : IConfigurationRegistry
	{
		private readonly IConfigurationRegistry parent;

		public ConfigurationRegistryWithGenericDefinitionFallback(IConfigurationRegistry parent)
		{
			this.parent = parent;
		}

		public ServiceConfiguration GetConfiguration(Type type, List<string> contracts)
		{
			var result = parent.GetConfiguration(type, contracts);
			if (result == null && type.IsGenericType)
				result = parent.GetConfiguration(type.GetDefinition(), contracts);
			return result;
		}

		public List<string> GetContractsUnionOrNull(string contract)
		{
			return parent.GetContractsUnionOrNull(contract);
		}
	}
}