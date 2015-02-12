using System;
using System.Collections.Generic;
using SimpleContainer.Configuration;
using SimpleContainer.Infection;

namespace SimpleContainer.Helpers
{
	internal static class InternalHelpers
	{
		public static string FormatContractsKey(List<string> contracts)
		{
			return contracts == null ? null : string.Join("->", contracts);
		}

		public static string NameOf<T>() where T : RequireContractAttribute, new()
		{
			return new T().ContractName;
		}

		public static List<string> ToInternalContracts(IEnumerable<string> contracts, Type type)
		{
			var requireContractAttribute = type.GetCustomAttributeOrNull<RequireContractAttribute>();
			if (contracts == null && requireContractAttribute == null)
				return null;
			var result = new List<string>();
			if (contracts != null)
				foreach (var contract in contracts)
					AddIfNotExists(contract, result);
			if (requireContractAttribute != null)
				AddIfNotExists(requireContractAttribute.ContractName, result);
			return result;
		}

		public static T GetConfiguration<T>(this IContainerConfigurationRegistry registry, Type type) where T : class
		{
			var result = registry.GetOrNull<T>(type);
			if (result == null && type.IsGenericType)
				result = registry.GetOrNull<T>(type.GetDefinition());
			return result;
		}

		private static void AddIfNotExists(string item, List<string> target)
		{
			if (target.IndexOf(item) < 0)
				target.Add(item);
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