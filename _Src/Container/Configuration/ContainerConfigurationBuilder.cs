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

		public ContainerConfigurationBuilder InheritorsOf(string description, Type baseType,
			Action<Type, ServiceConfigurationBuilder<object>> configure)
		{
			RegistryBuilder.Filtered(description, baseType, configure);
			return this;
		}

		public ContainerConfigurationBuilder InheritorsOf<T>(string description, Action<Type, ServiceConfigurationBuilder<object>> configure)
		{
			return InheritorsOf(description, typeof(T), configure);
		}

		public ContainerConfigurationBuilder ForAll(string description,
			Action<Type, ServiceConfigurationBuilder<object>> configure)
		{
			RegistryBuilder.Filtered(description, null, configure);
			return this;
		}
	}
}