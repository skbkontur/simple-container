using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Configuration
{
	internal class ServiceConfigurationSet
	{
		internal ServiceConfigurationSet parent;
		private List<Action> lazyConfigurators = new List<Action>();
		private List<ServiceConfiguration.Builder> builders = new List<ServiceConfiguration.Builder>();
		private volatile bool initialized;
		private readonly object lockObject = new object();
		private List<ServiceConfiguration> configurations;
		private Exception exception;
		private string errorMessage;

		public bool IsEmpty()
		{
			return lazyConfigurators.Count == 0 && builders.Count == 0;
		}

		public void SetDefaultComment(string defaultComment)
		{
			foreach (var b in builders)
				if (string.IsNullOrEmpty(b.Comment))
					b.SetComment(defaultComment);
		}

		public ServiceConfiguration GetConfiguration(List<string> contracts)
		{
			EnsureBuild();
			if (exception != null)
				throw new SimpleContainerException(errorMessage, exception);
			ServiceConfiguration result = null;
			var maxIndex = -1;
			foreach (var c in configurations)
			{
				var index = c.Contracts.GetSubsequenceLastIndex(contracts, StringComparer.OrdinalIgnoreCase);
				if (index > maxIndex)
				{
					maxIndex = index;
					result = c;
				}
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

		private void EnsureBuild()
		{
			if (!initialized)
				lock (lockObject)
					if (!initialized)
					{
						foreach (var configurator in lazyConfigurators)
							configurator();
						var newConfigurations = new List<ServiceConfiguration>();
						foreach (var b in builders)
							newConfigurations.Add(b.Build());
						builders = null;
						lazyConfigurators = null;
						initialized = true;
						if (exception != null)
							return;
						if (parent != null)
						{
							parent.EnsureBuild();
							if (parent.exception != null)
							{
								exception = parent.exception;
								errorMessage = parent.errorMessage;
								return;
							}
							foreach (var p in parent.configurations)
							{
								var overriden = false;
								foreach (var c in newConfigurations)
								{
									if (c.Contracts.SequenceEqual(p.Contracts, StringComparer.OrdinalIgnoreCase))
									{
										overriden = true;
										break;
									}
								}
								if (!overriden)
									newConfigurations.Add(p);
							}
						}
						newConfigurations.Sort(CompareByContractsCount.Intsance);
						configurations = newConfigurations;
					}
		}

		private class CompareByContractsCount : IComparer<ServiceConfiguration>
		{
			public static CompareByContractsCount Intsance { get; private set; }

			static CompareByContractsCount()
			{
				Intsance = new CompareByContractsCount();
			}

			public int Compare(ServiceConfiguration x, ServiceConfiguration y)
			{
				return y.Contracts.Count.CompareTo(x.Contracts.Count);
			}
		}
	}
}