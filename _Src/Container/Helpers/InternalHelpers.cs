using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
			var result = new List<string>();
			if (contracts != null)
				foreach (var contract in contracts)
					AddIfNotExists(contract, result);
			if (requireContractAttribute != null)
				AddIfNotExists(requireContractAttribute.ContractName, result);
			return result;
		}

		public static IEnumerable<Assembly> ReferencedAssemblies(this Assembly assembly, Func<AssemblyName, bool> assemblyFilter)
		{
			var referencedByAttribute = assembly.GetCustomAttributes<ContainerReferenceAttribute>()
				.Select(x => new AssemblyName(x.AssemblyName));
			return assembly.GetReferencedAssemblies()
				.Concat(referencedByAttribute)
				.Where(assemblyFilter)
				.Select(Assembly.Load);
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