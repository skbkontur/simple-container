using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	internal class ResolutionContext
	{
		private readonly IConfigurationRegistry configurationRegistry;
		private readonly List<ContainerService.Builder> stack = new List<ContainerService.Builder>();

		private readonly IDictionary<object, ContainerService.Builder> builderByToken =
			new Dictionary<object, ContainerService.Builder>();

		private readonly List<string> contracts = new List<string>();

		public ResolutionContext(IConfigurationRegistry configurationRegistry, string[] contracts)
		{
			this.configurationRegistry = configurationRegistry;
			if (contracts.Length > 0)
				PushContracts(contracts);
		}

		public string[] DeclaredContractNames()
		{
			return contracts.ToArray();
		}

		public int DeclaredContractsCount()
		{
			return contracts.Count;
		}

		public bool ContractDeclared(string name)
		{
			return contracts.Any(x => x.EqualsIgnoringCase(name));
		}

		public string DeclaredContractsKey()
		{
			return InternalHelpers.FormatContractsKey(DeclaredContractNames());
		}

		public ContainerService.Builder BuilderByToken(object token)
		{
			return builderByToken.GetOrDefault(token);
		}

		public ServiceConfiguration GetConfiguration(Type type)
		{
			var configurationSet = configurationRegistry.GetConfiguration(type);
			return configurationSet == null ? null : configurationSet.GetConfiguration(contracts);
		}

		public void Instantiate(ContainerService.Builder builder, SimpleContainer container, object id)
		{
			stack.Add(builder);
			builderByToken.Add(id, builder);
			builder.AttachToContext(this);
			var expandResult = TryExpandUnions();
			if (!expandResult.isOk)
				builder.SetError(expandResult.errorMessage);
			else if (expandResult.value != null)
			{
				var poppedContracts = contracts.PopMany(expandResult.value.Length);
				foreach (var c in expandResult.value.CartesianProduct())
				{
					var childService = ResolveInternal(builder.Type, c, container);
					if (!builder.LinkTo(childService))
						break;
				}
				contracts.AddRange(poppedContracts);
			}
			else
				container.Instantiate(builder);
			stack.RemoveLast();
			builderByToken.Remove(id);
		}

		private FuncResult<string[][]> TryExpandUnions()
		{
			string[][] result = null;
			var startIndex = 0;
			for (var i = 0; i < contracts.Count; i++)
			{
				var contract = contracts[i];
				var union = configurationRegistry.GetContractsUnionOrNull(contract);
				if (union == null)
				{
					if (result != null)
						result[i - startIndex] = new[] {contract};
				}
				else
				{
					if (result == null)
					{
						startIndex = i;
						result = new string[contracts.Count - startIndex][];
					}
					result[i - startIndex] = union.ToArray();
				}
			}
			return FuncResult.Ok(result);
		}

		public ContainerService.Builder GetTopService()
		{
			return stack.Count == 0 ? null : stack[stack.Count - 1];
		}

		public ContainerService.Builder GetPreviousService()
		{
			return stack.Count <= 1 ? null : stack[stack.Count - 2];
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
				var resultBuilder = container.NewService(type);
				resultBuilder.AttachToContext(this);
				resultBuilder.SetError(pushContractsResult.errorMessage);
				result = resultBuilder.Build();
			}
			else
				result = container.ResolveSingleton(type, this);
			contracts.RemoveLast(pushContractsResult.value);
			return result;
		}

		private FuncResult<int> PushContracts(string[] contractNames)
		{
			var pushedContractsCount = 0;
			foreach (var c in contractNames)
			{
				var duplicate = contracts.FirstOrDefault(x => x.EqualsIgnoringCase(c));
				if (duplicate != null)
				{
					const string messageFormat = "contract [{0}] already declared, all declared contracts [{1}]";
					return FuncResult.Fail<int>(messageFormat, c, DeclaredContractsKey());
				}
				contracts.Add(c);
				pushedContractsCount++;
			}
			return FuncResult.Ok(pushedContractsCount);
		}
	}
}