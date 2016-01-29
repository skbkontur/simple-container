using System;
using System.Linq;
using SimpleContainer.Helpers;

namespace SimpleContainer.Infection
{
	[AttributeUsage(
		AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Parameter | AttributeTargets.Field)]
	public class ContractsSequenceAttribute : RequireContractAttribute
	{
		public Type[] ContractAttributeTypes { get; private set; }

		public ContractsSequenceAttribute(params Type[] contractAttributeTypes)
			: base(contractAttributeTypes.Select(x => x.FormatName()).JoinStrings("-"))
		{
			ContractAttributeTypes = contractAttributeTypes;
		}
	}
}