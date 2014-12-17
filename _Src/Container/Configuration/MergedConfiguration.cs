using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleContainer.Configuration
{
	internal class MergedConfiguration : IContainerConfiguration
	{
		private readonly IContainerConfiguration parent;
		private readonly IContainerConfiguration child;

		public MergedConfiguration(IContainerConfiguration parent, IContainerConfiguration child)
		{
			this.parent = parent;
			this.child = child;
		}

		public T GetOrNull<T>(Type type) where T : class
		{
			return child.GetOrNull<T>(type) ?? parent.GetOrNull<T>(type);
		}

		public IEnumerable<ContractConfiguration> GetContractConfigurations(string contract)
		{
			return child.GetContractConfigurations(contract) ?? parent.GetContractConfigurations(contract);
		}

		public IEnumerable<string> DefaultContracts()
		{
			return parent.DefaultContracts().Union(child.DefaultContracts()).Distinct();
		}
	}
}