using System;
using SimpleContainer.Helpers;

namespace SimpleContainer.Interface
{
	public class ServiceName
	{
		public Type Type { get; private set; }
		public string[] Contracts { get; private set; }

		internal ServiceName(Type type, string[] contracts)
		{
			Type = type;
			Contracts = contracts;
		}

		public string Format()
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
	}
}