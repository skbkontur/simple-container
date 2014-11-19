using System;

namespace SimpleContainer.Infection
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Parameter)]
	public class RequireContractAttribute : Attribute
	{
		public string ContractName { get; private set; }

		public RequireContractAttribute(string contractName)
		{
			ContractName = contractName;
		}
	}
}