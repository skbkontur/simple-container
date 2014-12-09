using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Infection;

namespace SimpleContainer.Helpers
{
	internal static class InternalHelpers
	{
		//todo утащить во что-нить типа ContractsSet
		public static string FormatContractsKey(IEnumerable<string> contracts)
		{
			return string.Join("->", contracts);
		}

		public static List<string> ToInternalContracts(IEnumerable<string> contracts, Type type)
		{
			if (contracts == null)
				return null;
			var result = contracts.ToList();
			RequireContractAttribute requireContractAttribute;
			if (type.TryGetCustomAttribute(out requireContractAttribute))
				result.Add(requireContractAttribute.ContractName);
			return result;
		}

		public static string ByNameDependencyKey(string name)
		{
			return "name=" + name;
		}

		public static string ByTypeDependencyKey(Type type)
		{
			return "type=" + type.FormatName();
		}
	}
}