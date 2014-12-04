using System;
using System.Collections.Generic;

namespace SimpleContainer.Configuration
{
	public class ProfileConfigurationBuilder : AbstractConfigurationBuilder<ProfileConfigurationBuilder>
	{
		public ProfileConfigurationBuilder(ISet<Type> staticServices, bool isStaticConfiguration)
			: base(staticServices, isStaticConfiguration)
		{
		}

		internal ContainerConfiguration Build()
		{
			return new ContainerConfiguration(configurations, new Dictionary<string, ContractConfiguration>());
		}
	}
}