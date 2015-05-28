using System;
using System.Collections.Generic;

namespace SimpleContainer.Configuration
{
	internal class MergedConfiguration : IConfigurationRegistry
	{
		private readonly IConfigurationRegistry parent;
		private readonly IConfigurationRegistry child;

		public MergedConfiguration(IConfigurationRegistry parent, IConfigurationRegistry child)
		{
			this.parent = parent;
			this.child = child;
		}

		public Type[] GetGenericMappingsOrNull(Type type)
		{
			return child.GetGenericMappingsOrNull(type) ?? parent.GetGenericMappingsOrNull(type);
		}

		public ServiceConfiguration GetConfigurationOrNull(Type type, List<string> contracts)
		{
			return child.GetConfigurationOrNull(type, contracts) ?? parent.GetConfigurationOrNull(type, contracts);
		}

		public List<string> GetContractsUnionOrNull(string contract)
		{
			return child.GetContractsUnionOrNull(contract) ?? parent.GetContractsUnionOrNull(contract);
		}
	}
}