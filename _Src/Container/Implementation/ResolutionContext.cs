using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	internal class ResolutionContext : IContainerConfigurationRegistry
	{
		private readonly IContainerConfiguration configuration;
		private readonly List<ContainerService> current = new List<ContainerService>();
		private readonly List<ContractDeclaration> declaredContracts = new List<ContractDeclaration>();

		public ResolutionContext(IContainerConfiguration configuration, string[] contracts)
		{
			this.configuration = configuration;
			if (contracts.Length > 0)
				PushContracts(contracts);
		}

		public string[] DeclaredContractNames()
		{
			return declaredContracts.Select(x => x.name).ToArray();
		}

		public string[] GetDeclaredContractsByNames(List<string> names)
		{
			return declaredContracts
				.Where(x => names.Contains(x.name, StringComparer.OrdinalIgnoreCase))
				.Select(x => x.name)
				.ToArray();
		}

		public int DeclaredContractsCount()
		{
			return declaredContracts.Count;
		}

		public bool ContractDeclared(string name)
		{
			return declaredContracts.Any(x => x.name.EqualsIgnoringCase(name));
		}

		public string DeclaredContractsKey()
		{
			return InternalHelpers.FormatContractsKey(DeclaredContractNames());
		}

		public T GetOrNull<T>(Type type) where T : class
		{
			for (var i = declaredContracts.Count - 1; i >= 0; i--)
			{
				var declaration = declaredContracts[i];
				foreach (var definition in declaration.definitions)
				{
					var result = definition.GetOrNull<T>(type);
					if (result == null)
						continue;
					var containerService = GetTopService();
					containerService.UseContractWithName(declaration.name);
					foreach (var name in definition.RequiredContracts)
						containerService.UseContractWithName(name);
					return result;
				}
			}
			return configuration.GetOrNull<T>(type);
		}

		public bool DetectCycle(ContainerService containerService, SimpleContainer container, out ContainerService cycle)
		{
			if (!current.Contains(containerService))
			{
				cycle = null;
				return false;
			}
			var previous = current.Count == 0 ? null : current[current.Count - 1];
			var message = string.Format("cyclic dependency {0} ...-> {1} -> {0}",
				containerService.Type.FormatName(), previous == null ? "null" : previous.Type.FormatName());
			cycle = container.NewService(containerService.Type);
			cycle.SetError(message);
			return true;
		}

		public void Instantiate(ContainerService containerService, SimpleContainer container)
		{
			current.Add(containerService);
			containerService.AttachToContext(this);
			var expandResult = TryExpandUnions();
			if (!expandResult.isOk)
				containerService.SetError(expandResult.errorMessage);
			else if (expandResult.value != null)
			{
				var poppedContracts = declaredContracts.PopMany(expandResult.value.Length);
				foreach (var contracts in expandResult.value.CartesianProduct())
				{
					var childService = ResolveInternal(containerService.Type, contracts, container);
					if (!containerService.LinkTo(childService))
						break;
				}
				declaredContracts.AddRange(poppedContracts);
			}
			else
				container.Instantiate(containerService);
			containerService.EndInstantiate();
			current.RemoveAt(current.Count - 1);
		}

		private FuncResult<string[][]> TryExpandUnions()
		{
			string[][] result = null;
			var startIndex = 0;
			for (var i = 0; i < declaredContracts.Count; i++)
			{
				var declaredContract = declaredContracts[i];
				ContractConfiguration union = null;
				foreach (var definition in declaredContract.definitions)
				{
					if (definition.UnionContractNames == null)
						continue;
					if (union != null)
						return FuncResult.Fail<string[][]>(string.Format("contract [{0}] has conflicting unions [{1}] and [{2}]",
							declaredContract.name, InternalHelpers.FormatContractsKey(union.UnionContractNames),
							InternalHelpers.FormatContractsKey(definition.UnionContractNames)));
					union = definition;
				}
				if (union == null)
				{
					if (result != null)
						result[i - startIndex] = new[] {declaredContract.name};
				}
				else
				{
					if (result == null)
					{
						startIndex = i;
						result = new string[declaredContracts.Count - startIndex][];
					}
					result[i - startIndex] = union.UnionContractNames;
				}
			}
			return FuncResult.Ok(result);
		}

		public ContainerService GetTopService()
		{
			return current.Count == 0 ? null : current[current.Count - 1];
		}

		public ContainerService GetPreviousService()
		{
			return current.Count <= 1 ? null : current[current.Count - 2];
		}

		public ContainerService Resolve(Type type, IEnumerable<string> contractNames, SimpleContainer container)
		{
			var internalContracts = InternalHelpers.ToInternalContracts(contractNames, type);
			return internalContracts.Length == 0
				? container.ResolveSingleton(type, this)
				: ResolveInternal(type, internalContracts, container);
		}

		private ContainerService ResolveInternal(Type type, string[] contractNames, SimpleContainer container)
		{
			ContainerService result;
			var pushContractsResult = PushContracts(contractNames);
			if (!pushContractsResult.isOk)
			{
				result = container.NewService(type);
				result.AttachToContext(this);
				result.SetError(pushContractsResult.errorMessage);
			}
			else
				result = container.ResolveSingleton(type, this);
			declaredContracts.RemoveLast(pushContractsResult.value);
			return result;
		}

		private FuncResult<int> PushContracts(string[] contractNames)
		{
			var pushedContractsCount = 0;
			foreach (var c in contractNames)
			{
				var duplicate = declaredContracts.FirstOrDefault(x => x.name.EqualsIgnoringCase(c));
				if (duplicate != null)
				{
					const string messageFormat = "contract [{0}] already declared, all declared contracts [{1}]";
					return FuncResult.Fail<int>(messageFormat, c, DeclaredContractsKey());
				}
				var contractDeclaration = new ContractDeclaration
				{
					name = c,
					definitions = configuration.GetContractConfigurations(c)
						.EmptyIfNull()
						.Where(x => MatchWithDeclaredContracts(x.RequiredContracts))
						.ToList()
				};
				declaredContracts.Add(contractDeclaration);
				pushedContractsCount++;
			}
			return FuncResult.Ok(pushedContractsCount);
		}

		private bool MatchWithDeclaredContracts(List<string> required)
		{
			for (int r = 0, d = 0; r < required.Count; d++)
			{
				if (d >= declaredContracts.Count)
					return false;
				if (required[r].EqualsIgnoringCase(declaredContracts[d].name))
					r++;
			}
			return true;
		}

		private class ContractDeclaration
		{
			public string name;
			public List<ContractConfiguration> definitions;
		}
	}
}