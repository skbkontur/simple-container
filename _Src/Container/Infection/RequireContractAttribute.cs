using System;

namespace SimpleContainer.Infection
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface |
	                AttributeTargets.Parameter | AttributeTargets.Field)]
	public abstract class RequireContractAttribute : Attribute
	{
		public string ContractName { get; private set; }

		protected RequireContractAttribute(string contractName)
		{
			ContractName = contractName;
		}
	}
}