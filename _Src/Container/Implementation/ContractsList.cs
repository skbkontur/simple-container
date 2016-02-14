using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	internal class ContractsList
	{
		private List<string> contracts = new List<string>();
		private string[] contractsArray = InternalHelpers.emptyStrings;

		public int WeightOf(List<string> chain)
		{
			int i = 0, j = 0;
			while (true)
			{
				if (i >= chain.Count)
					return j;
				if (j >= contracts.Count)
					return -1;
				if (chain[i].EqualsIgnoringCase(contracts[j]))
					i++;
				j++;
			}
		}

		public int Count()
		{
			return contracts.Count;
		}

		public string[] Snapshot()
		{
			return contractsArray ?? (contractsArray = contracts.ToArray());
		}

		public List<string> Replace(string[] newContracts)
		{
			var oldContracts = contracts;
			if (newContracts.Length > 0 || contracts.Count > 0)
			{
				contracts = newContracts.ToList();
				contractsArray = newContracts;
			}
			return oldContracts;
		}

		public void Restore(List<string> oldContracts)
		{
			contracts = oldContracts;
			contractsArray = null;
		}

		public PushResult Push(string[] newContracts)
		{
			var pushedCount = 0;
			foreach (var newContract in newContracts)
			{
				foreach (var c in contracts)
					if (newContract.EqualsIgnoringCase(c))
						return new PushResult {isOk = false, duplicatedContractName = newContract, pushedContractsCount = pushedCount};
				contracts.Add(newContract);
				pushedCount++;
			}
			if (pushedCount > 0)
				contractsArray = null;
			return new PushResult {isOk = true, pushedContractsCount = pushedCount};
		}

		public struct PushResult
		{
			public bool isOk;
			public int pushedContractsCount;
			public string duplicatedContractName;
		}

		public void PushNoCheck(string[] newContracts)
		{
			contracts.AddRange(newContracts);
			if (newContracts.Length > 0)
				contractsArray = null;
		}

		public void RemoveLast(int count)
		{
			contracts.RemoveLast(count);
			if (count > 0)
				contractsArray = null;
		}

		public string[] PopMany(int count)
		{
			var result = contracts.PopMany(count);
			if (count > 0)
				contractsArray = null;
			return result;
		}

		public ExpandedUnions? TryExpandUnions(ConfigurationRegistry configuration)
		{
			ExpandedUnions? result = null;
			var startIndex = 0;
			for (var i = 0; i < contracts.Count; i++)
			{
				var contract = contracts[i];
				var union = configuration.GetContractsUnionOrNull(contract);
				if (union == null)
				{
					if (result != null)
						result.Value.contracts[i - startIndex] = new[] {contract};
				}
				else
				{
					if (result == null)
					{
						startIndex = i;
						result = new ExpandedUnions
						{
							contracts = new string[contracts.Count - startIndex][],
							unionedContracts = new List<UnionedContact>(1)
						};
					}
					result.Value.unionedContracts.Add(new UnionedContact {children = union, parent = contract});
					result.Value.contracts[i - startIndex] = union.ToArray();
				}
			}
			return result;
		}
	}
}