﻿using System;
using System.Collections.Generic;
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

		public static string[] ParseContracts(ICustomAttributeProvider provider, bool inverse)
		{
			var attributes = provider.GetCustomAttributes<RequireContractAttribute>();
			if (attributes.Length == 0)
				return emptyStrings;
			var contractsFromAttributes = new string[attributes.Length];
			for (var i = 0; i < attributes.Length; i++)
				contractsFromAttributes[i] = attributes[inverse ? attributes.Length - i - 1 : i].ContractName;
			return contractsFromAttributes;
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
		public static readonly List<Type> emptyTypesList = new List<Type>(0);

		public static string DumpValue(object value)
		{
			if (value == null)
				return "<null>";
			var result = value.ToString();
			return value is bool ? result.ToLower() : result;
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