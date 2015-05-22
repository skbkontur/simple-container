using System.Collections.Generic;

namespace SimpleContainer.Configuration
{
	internal interface IServiceConfigurationSet
	{
		ServiceConfiguration GetConfiguration(List<string> contracts);
	}
}