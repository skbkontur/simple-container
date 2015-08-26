using System;
using SimpleContainer.Helpers;

namespace SimpleContainer.Interface
{
	public struct ServiceName : IEquatable<ServiceName>
	{
		private readonly Type type;
		private readonly string[] contracts;

		public ServiceName(Type type, string[] contracts)
		{
			this.type = type;
			this.contracts = contracts;
		}

		internal static ServiceName Parse(Type type, bool excludeTypeContractIfDuplicates, params string[] contracts)
		{
			var typeContracts = InternalHelpers.ParseContracts(type);
			var contractsToUse = excludeTypeContractIfDuplicates && contracts.Length > 0 && typeContracts.Length == 1 &&
			                     contracts[contracts.Length - 1].EqualsIgnoringCase(typeContracts[0])
				? contracts
				: contracts.Concat(typeContracts);
			return new ServiceName(type, contractsToUse);
		}

		public Type Type
		{
			get { return type; }
		}

		public string[] Contracts
		{
			get { return contracts; }
		}

		public ServiceName AddContracts(params string[] contract)
		{
			return new ServiceName(type, contracts.Concat(contract));
		}

		public override string ToString()
		{
			return FormatTypeName() + FormatContracts();
		}

		public string FormatTypeName()
		{
			return Type.FormatName();
		}

		public string FormatContracts()
		{
			return Contracts.IsEmpty() ? "" : "[" + InternalHelpers.FormatContractsKey(Contracts) + "]";
		}

		public bool Equals(ServiceName other)
		{
			if (Type != other.Type)
				return false;
			if (Contracts.Length != other.Contracts.Length)
				return false;
			for (var i = 0; i < Contracts.Length; i++)
				if (!string.Equals(Contracts[i], other.Contracts[i], StringComparison.OrdinalIgnoreCase))
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
				foreach (var contract in Contracts)
					result = Utils.CombineHashCodes(result, contract.GetHashCode());
				return (Type.GetHashCode()*397) ^ result;
			}
		}
	}
}