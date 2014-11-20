using System;
using SimpleContainer.Configuration;

namespace SimpleContainer.Helpers
{
	public static class InternalHelpers
	{
		public static IContainerConfiguration Extend(this IContainerConfiguration configuration,
			Action<ContainerConfigurationBuilder> action)
		{
			var builder = new ContainerConfigurationBuilder();
			action(builder);
			return new MergedConfiguration(configuration, builder.Build());
		}
	}
}