using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Configuration
{
	internal class ServiceConfigurationSet : IServiceConfigurationSet
	{
		private List<Action> lazyConfigurators = new List<Action>();
		private List<ServiceConfiguration.Builder> builders = new List<ServiceConfiguration.Builder>();
		private volatile bool initialized;
		private readonly object lockObject = new object();
		private ServiceConfiguration[] configurations;
		private Exception exception;
		private string errorMessage;

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
			if (exception != null)
				throw new SimpleContainerException(errorMessage, exception);
			ServiceConfiguration result = null;
			var maxIndex = -1;
			foreach (var c in configurations)
			{
				var index = c.Contracts.GetSubsequenceLastIndex(contracts, StringComparer.OrdinalIgnoreCase);
				if (index < 0 || result != null && index <= maxIndex)
					continue;
				maxIndex = index;
				result = c;
			}
			return result;
		}

		public void SetError(string newErrorMessage, Exception newException)
		{
			errorMessage = newErrorMessage;
			exception = newException;
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