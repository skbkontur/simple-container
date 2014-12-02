using System;
using System.Collections.Generic;
using SimpleContainer.Configuration;

namespace SimpleContainer.Helpers
{
	internal static class InternalHelpers
	{
		public static IContainerConfiguration Extend(this IContainerConfiguration configuration,
			Action<ContainerConfigurationBuilder> action)
		{
			var builder = new ContainerConfigurationBuilder();
			action(builder);
			return new MergedConfiguration(configuration, builder.Build());
		}

		//todo утащить во что-нить типа ContractsSet
		public static string FormatContractsKey(IEnumerable<string> contracts)
		{
			return string.Join("->", contracts);
		}
	}
}