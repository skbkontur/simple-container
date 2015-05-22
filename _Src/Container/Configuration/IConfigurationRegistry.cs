using System;
using System.Collections.Generic;

namespace SimpleContainer.Configuration
{
	internal interface IConfigurationRegistry
	{
		IServiceConfigurationSet GetConfiguration(Type type);
		List<string> GetContractsUnionOrNull(string contract);
	}
}