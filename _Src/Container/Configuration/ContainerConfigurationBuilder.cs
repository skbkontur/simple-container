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

		public ContainerConfigurationBuilder RegisterImplementationSelector(ImplementationSelector s)
		{
			RegistryBuilder.RegisterImplementationSelector(s);
			return this;
		}
	}
}