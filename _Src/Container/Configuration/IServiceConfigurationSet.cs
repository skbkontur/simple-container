using System;
using System.Collections.Generic;

namespace SimpleContainer.Configuration
{
	internal interface IServiceConfigurationSet
	{
		ServiceConfiguration GetConfiguration(List<string> contracts);
		IServiceConfigurationSet CloneWithFilter(Func<Type, bool> filter);
	}
}