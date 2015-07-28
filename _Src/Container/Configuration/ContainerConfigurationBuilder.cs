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

		public ContainerConfigurationBuilder InheritorsOf(Type baseType,
			Action<Type, ServiceConfigurationBuilder<object>> configure)
		{
			RegistryBuilder.Filtered(baseType, configure);
			return this;
		}

		public ContainerConfigurationBuilder InheritorsOf<T>(Action<Type, ServiceConfigurationBuilder<object>> configure)
		{
			return InheritorsOf(typeof (T), configure);
		}

		public ContainerConfigurationBuilder ForAll(Action<Type, ServiceConfigurationBuilder<object>> configure)
		{
			RegistryBuilder.Filtered(null, configure);
			return this;
		}
	}
}