using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Configuration;
using SimpleContainer.Implementation;
using SimpleContainer.Infection;

namespace SimpleContainer.Helpers
{
	internal static class InternalHelpers
	{
		public static bool IsGood(this ServiceStatus status)
		{
			return status == ServiceStatus.Ok || status == ServiceStatus.NotResolved;
		}

		public static bool IsBad(this ServiceStatus status)
		{
			return status == ServiceStatus.Error || status == ServiceStatus.DependencyError;
		}

		public static string FormatContractsKey(IEnumerable<string> contracts)
		{
			return contracts == null ? null : string.Join("->", contracts);
		}

		public static string NameOf<T>() where T : RequireContractAttribute, new()
		{
			return new T().ContractName;
		}

		public static readonly string[] emptyStrings = new string[0];

		public static string[] ToInternalContracts(IEnumerable<string> contracts, Type type)
		{
			var attribute = type.GetCustomAttributeOrNull<RequireContractAttribute>();
			if (attribute == null)
				return contracts == null ? emptyStrings : contracts.ToArray();
			if (contracts == null)
				return new[] {attribute.ContractName};
			var result = contracts.ToList();
			result.Add(attribute.ContractName);
			return result.ToArray();
		}

		public static T GetConfiguration<T>(this IContainerConfigurationRegistry registry, Type type) where T : class
		{
			var result = registry.GetOrNull<T>(type);
			if (result == null && type.IsGenericType)
				result = registry.GetOrNull<T>(type.GetDefinition());
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