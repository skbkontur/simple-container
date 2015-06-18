using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class ResolutionContext
	{
		private readonly List<ContainerService.Builder> stack = new List<ContainerService.Builder>();
		private readonly HashSet<ServiceName> constructingServices = new HashSet<ServiceName>();

		public List<string> Contracts { get; private set; }
		public SimpleContainer Container { get; private set; }

		public ResolutionContext(SimpleContainer container, string[] contracts)
		{
			Container = container;
			Contracts = new List<string>();
			if (contracts.Length > 0)
				PushContracts(contracts);
		}

		public string DeclaredContractsKey()
		{
			return InternalHelpers.FormatContractsKey(Contracts);
		}

		public ContainerService Instantiate(Type type, IObjectAccessor arguments)
		{
			var builder = new ContainerService.Builder(type, this);
			if (arguments != null)
				builder.NeedNewInstance(arguments);
			var declaredName = builder.GetDeclaredName();
			if (!constructingServices.Add(declaredName))
			{
				var previous = GetTopService();
				var message = string.Format("cyclic dependency {0} ...-> {1} -> {0}",
					type.FormatName(), previous == null ? "null" : previous.Type.FormatName());
				var cycleBuilder = new ContainerService.Builder(type, this);
				cycleBuilder.SetError(message);
				return cycleBuilder.Build();
			}
			stack.Add(builder);
			var expandResult = TryExpandUnions(Container.Configuration);
			if (expandResult != null)
			{
				var poppedContracts = Contracts.PopMany(expandResult.Length);
				foreach (var c in expandResult.CartesianProduct())
				{
					var childService = ResolveInternal(builder.Type, c);
					builder.LinkTo(childService, null);
					if (builder.Status.IsBad())
						break;
				}
				Contracts.AddRange(poppedContracts);
			}
			else
				Container.Instantiate(builder);
			stack.RemoveLast();
			constructingServices.Remove(declaredName);
			if (builder.Status == ServiceStatus.Ok && arguments != null)
			{
				var unused = arguments.GetUnused().ToArray();
				if (unused.Any())
					builder.SetError(string.Format("arguments [{0}] are not used", unused.JoinStrings(",")));
			}
			return builder.Build();
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

		public ContainerService Resolve(Type type, IEnumerable<string> contractNames)
		{
			var internalContracts = InternalHelpers.ToInternalContracts(contractNames, type);
			return internalContracts.Length == 0
				? Container.ResolveSingleton(type, this)
				: ResolveInternal(type, internalContracts);
		}

		private ContainerService ResolveInternal(Type type, string[] contractNames)
		{
			ContainerService result;
			var pushContractsResult = PushContracts(contractNames);
			if (!pushContractsResult.isOk)
			{
				var resultBuilder = new ContainerService.Builder(type, this);
				if (resultBuilder.Status == ServiceStatus.Ok)
					resultBuilder.SetError(pushContractsResult.errorMessage);
				result = resultBuilder.Build();
			}
			else
				result = Container.ResolveSingleton(type, this);
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