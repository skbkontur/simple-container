using System;
using System.Collections.Generic;

namespace SimpleContainer.Configuration
{
	internal interface IConfigurationRegistry
	{
		ServiceConfiguration GetConfiguration(Type type, List<string> contracts);
		List<string> GetContractsUnionOrNull(string contract);
	}
}