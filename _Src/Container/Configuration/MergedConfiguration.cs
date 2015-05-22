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

		public IServiceConfigurationSet GetConfiguration(Type type)
		{
			return child.GetConfiguration(type) ?? parent.GetConfiguration(type);
		}

		public List<string> GetContractsUnionOrNull(string contract)
		{
			return child.GetContractsUnionOrNull(contract) ?? parent.GetContractsUnionOrNull(contract);
		}
	}
}