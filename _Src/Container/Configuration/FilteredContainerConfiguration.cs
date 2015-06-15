using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Interface;

namespace SimpleContainer.Configuration
{
	//гнусный экспериментальный хак, нужен тока для GenericConfigurator-а
	//выпилить, когда с generic-ами нормальная схема будет
	internal class FilteredContainerConfiguration : IConfigurationRegistry
	{
		private readonly IConfigurationRegistry parent;

		private readonly IDictionary<ServiceNameForListContracts, ServiceConfiguration> filteredCache =
			new Dictionary<ServiceNameForListContracts, ServiceConfiguration>();

		private readonly Func<Type, bool> filter;

		public FilteredContainerConfiguration(IConfigurationRegistry parent, Func<Type, bool> filter)
		{
			this.parent = parent;
			this.filter = filter;
		}

		public Type[] GetGenericMappingsOrNull(Type type)
		{
			var result = parent.GetGenericMappingsOrNull(type);
			return result == null ? null : result.Where(filter).ToArray();
		}

		public ServiceConfiguration GetConfigurationOrNull(Type type, List<string> contracts)
		{
			var key = new ServiceNameForListContracts(type, contracts);
			ServiceConfiguration result;
			if (!filteredCache.TryGetValue(key, out result))
			{
				result = parent.GetConfigurationOrNull(type, contracts);
				if (result != null)
					result = result.CloneWithFilter(filter);
				filteredCache.Add(key, result);
			}
			return result;
		}

		public List<string> GetContractsUnionOrNull(string contract)
		{
			return parent.GetContractsUnionOrNull(contract);
		}

		public ImplementationFilter[] GetImplementationFilters()
		{
			return parent.GetImplementationFilters();
		}

		//todo get rid of this shit
		private struct ServiceNameForListContracts : IEquatable<ServiceNameForListContracts>
		{
			private readonly Type type;
			private readonly List<string> contracts;

			internal ServiceNameForListContracts(Type type, List<string> contracts)
			{
				this.type = type;
				this.contracts = contracts;
			}

			public bool Equals(ServiceNameForListContracts other)
			{
				if (type != other.type)
					return false;
				if (contracts.Count != other.contracts.Count)
					return false;
				for (var i = 0; i < contracts.Count; i++)
					if (!string.Equals(contracts[i], other.contracts[i], StringComparison.OrdinalIgnoreCase))
						return false;
				return true;
			}

			public override bool Equals(object obj)
			{
				return !ReferenceEquals(null, obj) && obj.GetType() == GetType() && Equals((ServiceName) obj);
			}

			public override int GetHashCode()
			{
				unchecked
				{
					var result = 0;
					foreach (var contract in contracts)
						result = CombineHashCodes(result, contract.GetHashCode());
					return (type.GetHashCode()*397) ^ result;
				}
			}

			private static int CombineHashCodes(int h1, int h2)
			{
				return ((h1 << 5) + h1) ^ h2;
			}
		}
	}
}