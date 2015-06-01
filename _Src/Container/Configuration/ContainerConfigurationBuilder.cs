using System;
using System.Collections.Generic;

namespace SimpleContainer.Configuration
{
	public class ContainerConfigurationBuilder : AbstractConfigurationBuilder<ContainerConfigurationBuilder>
	{
		public StaticServicesConfigurator StaticServices { get; private set; }

		public ContainerConfigurationBuilder(bool isStatic)
			: base(new ConfigurationRegistry.Builder(), new List<string>())
		{
			StaticServices = new StaticServicesConfigurator(isStatic);
		}

		public ContainerConfigurationBuilder MakeStatic(Type type)
		{
			StaticServices.MakeStatic(type);
			return this;
		}
	}
}