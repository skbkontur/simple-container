using System;
using System.Collections.Generic;

namespace SimpleContainer.Configuration
{
	//гнусный экспериментальный хак, нужен тока для GenericConfigurator-а
	//выпилить, когда с generic-ами нормальная схема будет
	internal class FilteredContainerConfiguration : IContainerConfiguration
	{
		private readonly IContainerConfiguration parent;
		private readonly IDictionary<Type, object> filteredCache = new Dictionary<Type, object>();
		private readonly Func<Type, bool> filter;

		public FilteredContainerConfiguration(IContainerConfiguration parent, Func<Type, bool> filter)
		{
			this.parent = parent;
			this.filter = filter;
		}

		public T GetOrNull<T>(Type type) where T : class
		{
			var result = parent.GetOrNull<T>(type);
			if (result == null)
				return null;
			var interfaceConfiguration = result as InterfaceConfiguration;
			if (interfaceConfiguration == null || interfaceConfiguration.ImplementationTypes == null)
				return result;
			object resultObject;
			if (!filteredCache.TryGetValue(type, out resultObject))
				filteredCache.Add(type, resultObject = interfaceConfiguration.CloneWithFilter(filter));
			return (T) resultObject;
		}

		public ContractConfiguration[] GetContractConfigurations(string contract)
		{
			return parent.GetContractConfigurations(contract);
		}
	}
}