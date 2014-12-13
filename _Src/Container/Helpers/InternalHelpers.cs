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
			return contracts == null ? null : string.Join("->", contracts);
		}

		public static List<string> ToInternalContracts(IEnumerable<string> contracts, Type type)
		{
			var requireContractAttribute = type.GetCustomAttributeOrNull<RequireContractAttribute>();
			if (contracts == null && requireContractAttribute == null)
				return null;
			var result = contracts.ToList();
			if (requireContractAttribute != null)
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