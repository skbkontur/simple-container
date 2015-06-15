using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	internal class ResolutionContext
	{
		private readonly List<ContainerService.Builder> stack = new List<ContainerService.Builder>();

		private readonly IDictionary<object, ContainerService.Builder> builderById =
			new Dictionary<object, ContainerService.Builder>();

		public List<string> Contracts { get; private set; }

		public ResolutionContext(string[] contracts)
		{
			Contracts = new List<string>();
			if (contracts.Length > 0)
				PushContracts(contracts);
		}

		public string DeclaredContractsKey()
		{
			return InternalHelpers.FormatContractsKey(Contracts);
		}

		public ContainerService.Builder BuilderByToken(object token)
		{
			return builderById.GetOrDefault(token);
		}

		public void Instantiate(ContainerService.Builder builder, object id)
		{
			if (builder.Status != ServiceStatus.Ok)
				return;
			stack.Add(builder);
			if (id != null)
				builderById.Add(id, builder);
			var expandResult = TryExpandUnions(builder.Container.Configuration);
			if (expandResult != null)
			{
				var poppedContracts = Contracts.PopMany(expandResult.Length);
				foreach (var c in expandResult.CartesianProduct())
				{
					var childService = ResolveInternal(builder.Type, c, builder.Container);
					builder.LinkTo(childService);
					if (builder.Status.IsBad())
						break;
				}
				Contracts.AddRange(poppedContracts);
			}
			else
				builder.Container.Instantiate(builder);
			stack.RemoveLast();
			if (id != null)
				builderById.Remove(id);
		}

		private string[][] TryExpandUnions(IConfigurationRegistry configuration)
		{
			string[][] result = null;
			var startIndex = 0;
			for (var i = 0; i < Contracts.Count; i++)
			{
				var contract = Contracts[i];
				var union = configuration.GetContractsUnionOrNull(contract);
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
						result = new string[Contracts.Count - startIndex][];
					}
					result[i - startIndex] = union.ToArray();
				}
			}
			return result;
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
				var resultBuilder = new ContainerService.Builder(type, container, this);
				if (resultBuilder.Status == ServiceStatus.Ok)
					resultBuilder.SetError(pushContractsResult.errorMessage);
				result = resultBuilder.Build();
			}
			else
				result = container.ResolveSingleton(type, this);
			Contracts.RemoveLast(pushContractsResult.value);
			return result;
		}

		private ValueOrError<int> PushContracts(string[] contractNames)
		{
			var pushedContractsCount = 0;
			foreach (var c in contractNames)
			{
				var duplicate = Contracts.FirstOrDefault(x => x.EqualsIgnoringCase(c));
				if (duplicate != null)
				{
					const string messageFormat = "contract [{0}] already declared, all declared contracts [{1}]";
					return ValueOrError.Fail<int>(messageFormat, c, DeclaredContractsKey());
				}
				Contracts.Add(c);
				pushedContractsCount++;
			}
			return ValueOrError.Ok(pushedContractsCount);
		}
	}
}