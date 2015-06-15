using System;
using System.Collections.Generic;

namespace SimpleContainer.Configuration
{
	internal interface IConfigurationRegistry
	{
		Type[] GetGenericMappingsOrNull(Type type);
		ServiceConfiguration GetConfigurationOrNull(Type type, List<string> contracts);
		List<string> GetContractsUnionOrNull(string contract);
		ImplementationFilter[] GetImplementationFilters();
	}
}