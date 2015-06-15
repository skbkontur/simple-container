using System;
using System.Collections.Generic;

namespace SimpleContainer.Configuration
{
	public class ContainerConfigurationBuilder : AbstractConfigurationBuilder<ContainerConfigurationBuilder>
	{
		public ContainerConfigurationBuilder()
			: base(new ConfigurationRegistry.Builder(), new List<string>())
		{
		}

		public ContainerConfigurationBuilder RegisterImplementationFilter(string name, Func<Type, Type, bool> f)
		{
			RegistryBuilder.RegisterImplementationFilter(name, f);
			return this;
		}
	}
}