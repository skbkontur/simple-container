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

		public Type[] GetGenericMappingsOrNull(Type type)
		{
			return parent.GetGenericMappingsOrNull(type);
		}

		public ServiceConfiguration GetConfigurationOrNull(Type type, List<string> contracts)
		{
			var result = parent.GetConfigurationOrNull(type, contracts);
			if (result == null && type.IsGenericType)
				result = parent.GetConfigurationOrNull(type.GetDefinition(), contracts);
			return result;
		}

		public List<string> GetContractsUnionOrNull(string contract)
		{
			return parent.GetContractsUnionOrNull(contract);
		}
	}
}