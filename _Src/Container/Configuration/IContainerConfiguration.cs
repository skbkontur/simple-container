using System;
using System.Collections.Generic;

namespace SimpleContainer.Configuration
{
	internal interface IContainerConfiguration
	{
		T GetOrNull<T>(Type type) where T : class;
		IEnumerable<ContractConfiguration> GetContractConfigurations(string contract);
	}
}