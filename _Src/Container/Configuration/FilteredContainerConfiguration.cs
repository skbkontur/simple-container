using System;
using System.Collections.Generic;

namespace SimpleContainer.Configuration
{
	//гнусный экспериментальный хак, нужен тока для GenericConfigurator-а
	//выпилить, когда с generic-ами нормальная схема будет
	internal class FilteredContainerConfiguration : IConfigurationRegistry
	{
		private readonly IConfigurationRegistry parent;

		private readonly IDictionary<Type, IServiceConfigurationSet> filteredCache =
			new Dictionary<Type, IServiceConfigurationSet>();

		private readonly Func<Type, bool> filter;

		public FilteredContainerConfiguration(IConfigurationRegistry parent, Func<Type, bool> filter)
		{
			this.parent = parent;
			this.filter = filter;
		}

		public IServiceConfigurationSet GetConfiguration(Type type)
		{
			IServiceConfigurationSet result;
			if (!filteredCache.TryGetValue(type, out result))
			{
				result = parent.GetConfiguration(type);
				if (result != null)
					result = result.CloneWithFilter(filter);
				filteredCache.Add(type, result);
			}
			return result;
		}

		public List<string> GetContractsUnionOrNull(string contract)
		{
			return parent.GetContractsUnionOrNull(contract);
		}
	}
}