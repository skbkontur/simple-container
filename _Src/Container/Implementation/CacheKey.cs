using System;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	internal struct CacheKey : IEquatable<CacheKey>
	{
		public readonly Type type;
		public readonly string[] contracts;
		public readonly string contractsKey;

		public CacheKey(Type type, string[] contracts)
		{
			this.type = type;
			this.contracts = contracts ?? InternalHelpers.emptyStrings;
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