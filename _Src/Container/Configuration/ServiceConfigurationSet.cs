using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Implementation;
using SimpleContainer.Interface;

namespace SimpleContainer.Configuration
{
	internal class ServiceConfigurationSet
	{
		internal ServiceConfigurationSet parent;
		private List<Action> lazyConfigurators = new List<Action>();
		private List<ServiceConfiguration.Builder> builders = new List<ServiceConfiguration.Builder>();
		private volatile bool built;
		private readonly object buildLock = new object();
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

		public ServiceConfiguration GetConfiguration(ContractsList contracts)
		{
			EnsureBuilt();
			if (exception != null)
				throw new SimpleContainerException(errorMessage, exception);
			ServiceConfiguration result = null;
			var maxWeight = -1;
			foreach (var c in configurations)
			{
				var weight = contracts.Match(c.Contracts);
				if (weight > maxWeight)
				{
					maxWeight = weight;
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
			if (built)
				throw new InvalidOperationException("assertion failure");
			lazyConfigurators.Add(configurator);
		}

		public ServiceConfiguration.Builder GetBuilder(List<string> contracts)
		{
			if (built)
				throw new InvalidOperationException("assertion failure");
			var result = builders.FirstOrDefault(x => x.Contracts.SequenceEqual(contracts, StringComparer.OrdinalIgnoreCase));
			if (result == null)
				builders.Add(result = new ServiceConfiguration.Builder(contracts));
			return result;
		}

		private void EnsureBuilt()
		{
			if (!built)
				lock (buildLock)
					if (!built)
					{
						foreach (var configurator in lazyConfigurators)
							configurator();
						var newConfigurations = new List<ServiceConfiguration>();
						foreach (var b in builders)
							newConfigurations.Add(b.Build());
						builders = null;
						lazyConfigurators = null;
						built = true;
						if (exception != null)
							return;
						if (parent != null)
						{
							parent.EnsureBuilt();
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