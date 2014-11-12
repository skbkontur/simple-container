using System;

namespace SimpleContainer.Infection
{
	public class RequireContractAttribute : Attribute
	{
		public string ContractName { get; private set; }

		public RequireContractAttribute(string contractName)
		{
			ContractName = contractName;
		}
	}
}