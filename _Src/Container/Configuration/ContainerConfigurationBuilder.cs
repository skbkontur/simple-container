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

		public ServiceConfigurationBuilder<object> InheritorOf(Type baseType)
		{
			return new ServiceConfigurationBuilder<object>(RegistryBuilder.InheritorOf(baseType));
		}

		public ServiceConfigurationBuilder<object> InheritorOf<T>()
		{
			return InheritorOf(typeof (T));
		}
	}
}