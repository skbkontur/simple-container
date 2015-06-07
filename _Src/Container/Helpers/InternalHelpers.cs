using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

		public static ValueOrError<ConstructorInfo> GetConstructor(this Type target)
		{
			var allConstructors = target.GetConstructors();
			ConstructorInfo publicConstructor = null;
			ConstructorInfo containerConstructor = null;
			var hasManyPublicConstructors = false;
			foreach (var constructor in allConstructors)
			{
				if (!constructor.IsPublic)
					continue;
				if (publicConstructor != null)
					hasManyPublicConstructors = true;
				else
					publicConstructor = constructor;
				if (constructor.IsDefined("ContainerConstructorAttribute"))
				{
					if (containerConstructor != null)
						return ValueOrError.Fail<ConstructorInfo>("many ctors with [ContainerConstructor] attribute");
					containerConstructor = constructor;
				}
			}
			if (containerConstructor != null)
				return ValueOrError.Ok(containerConstructor);
			if (hasManyPublicConstructors)
				return ValueOrError.Fail<ConstructorInfo>("many public ctors");
			return publicConstructor == null
				? ValueOrError.Fail<ConstructorInfo>("no public ctors")
				: ValueOrError.Ok(publicConstructor);
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