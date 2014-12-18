using System;
using System.Collections.Generic;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	internal struct CacheKey : IEquatable<CacheKey>
	{
		public readonly Type type;
		public readonly List<string> contracts;
		public readonly string contractsKey;

		public CacheKey(Type type, List<string> contracts)
		{
			this.type = type;
			this.contracts = contracts ?? new List<string>(0);
			contractsKey = InternalHelpers.FormatContractsKey(this.contracts);
		}

		public bool Equals(CacheKey other)
		{
			return type == other.type && contractsKey == other.contractsKey;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			return obj is CacheKey && Equals((CacheKey) obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (type.GetHashCode()*397) ^ (contractsKey == null ? 0 : contractsKey.GetHashCode());
			}
		}

		public static bool operator ==(CacheKey left, CacheKey right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(CacheKey left, CacheKey right)
		{
			return !left.Equals(right);
		}
	}
}