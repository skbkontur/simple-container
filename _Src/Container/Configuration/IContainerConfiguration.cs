using System;

namespace SimpleContainer.Configuration
{
	internal interface IContainerConfiguration
	{
		T GetOrNull<T>(Type type) where T : class;
		ContractConfiguration GetContractConfiguration(string contract);
	}
}