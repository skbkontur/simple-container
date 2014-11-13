using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;

namespace SimpleContainer.Configuration
{
	public class ProfileConfiguratorFactory
	{
		private readonly IDescribeConfigurator configuratorDescriber;
		private readonly IContainer container;

		public ProfileConfiguratorFactory(IEnumerable<Type> types, IDescribeConfigurator configuratorDescriber)
			: this(StupidContainerHelpers.CreateContainer(types), configuratorDescriber)
		{
		}

		public ProfileConfiguratorFactory(IContainer container, IDescribeConfigurator configuratorDescriber)
		{
			this.container = container;
			this.configuratorDescriber = configuratorDescriber;
		}

		public IHandleProfile<IProfile> GetConfigurator(string profileName)
		{
			var profileType = GetProfileType(profileName);
			return ConfiguratorBuilder.Create(container, configuratorDescriber, profileType).Build();
		}

		private Type GetProfileType(string profileName)
		{
			Type result;
			if (!container.GetImplementationsOf<IProfile>()
				     .Where(t => t.Name == profileName + "Profile" && typeof (IProfile).IsAssignableFrom(t))
				     .TrySingle(out result))
				throw new InvalidOperationException(string.Format("Can't find profile '{0}'", profileName));
			return result;
		}
	}
}