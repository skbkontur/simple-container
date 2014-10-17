using System;
using System.Reflection;

namespace SimpleContainer.Configuration
{
	public class ConfiguratorDescriber : IDescribeConfigurator
	{
		private readonly Assembly primaryAssembly;

		public ConfiguratorDescriber(Assembly primaryAssembly)
		{
			this.primaryAssembly = primaryAssembly;
		}

		public bool IsPrimary(Type configuratorType)
		{
			return configuratorType.Assembly == primaryAssembly;
		}
	}
}