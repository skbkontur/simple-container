using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;

namespace SimpleContainer.Configuration
{
	internal class ServiceConfigurationSet : IServiceConfigurationSet
	{
		private List<Action> lazyConfigurators = new List<Action>();
		private List<ServiceConfiguration.Builder> builders = new List<ServiceConfiguration.Builder>();
		private volatile bool initialized;
		private readonly object lockObject = new object();
		private ServiceConfiguration[] configurations;

		public ServiceConfiguration GetConfiguration(List<string> contracts)
		{
			if (!initialized)
				lock (lockObject)
					if (!initialized)
					{
						foreach (var configurator in lazyConfigurators)
							configurator();
						configurations = builders.Select(x => x.Build()).OrderByDescending(x => x.Contracts.Count).ToArray();
						builders = null;
						lazyConfigurators = null;
						initialized = true;
					}
			return configurations.FirstOrDefault(x => contracts.IsSubsequenceOf(x.Contracts, StringComparer.OrdinalIgnoreCase));
		}

		public IServiceConfigurationSet CloneWithFilter(Func<Type, bool> filter)
		{
			return new ServiceConfigurationSet
			{
				configurations = configurations.Select(x => x.CloneWithFilter(filter)).ToArray(),
				initialized = true
			};
		}

		public void RegisterLazyConfigurator(Action configurator)
		{
			if (initialized)
				throw new InvalidOperationException("assertion failure");
			lazyConfigurators.Add(configurator);
		}

		public ServiceConfiguration.Builder GetBuilder(List<string> contracts)
		{
			if (initialized)
				throw new InvalidOperationException("assertion failure");
			var result = builders.FirstOrDefault(x => x.Contracts.SequenceEqual(contracts, StringComparer.OrdinalIgnoreCase));
			if (result == null)
				builders.Add(result = new ServiceConfiguration.Builder(contracts));
			return result;
		}
	}
}