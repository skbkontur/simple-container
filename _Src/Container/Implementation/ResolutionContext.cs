using System;
using System.Collections.Generic;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class ResolutionContext
	{
		private readonly List<ContainerService.Builder> stack = new List<ContainerService.Builder>();
		private readonly HashSet<Type> constructingServices = new HashSet<Type>();

		public List<string> Contracts { get; private set; }
		public SimpleContainer Container { get; private set; }

		public bool AnalizeDependenciesOnly { get; set; }

		public ResolutionContext(SimpleContainer container, string[] contracts)
		{
			Container = container;
			Contracts = new List<string>(contracts);
		}

		public ContainerService Create(Type type, List<string> contracts, object arguments)
		{
			List<string> oldContracts = null;
			if (contracts != null)
			{
				oldContracts = Contracts;
				Contracts = contracts;
			}
			var result = Container.ResolveSingleton(new ServiceName(type, InternalHelpers.emptyStrings), true,
				ObjectAccessor.Get(arguments), this);
			if (contracts != null)
				Contracts = oldContracts;
			return result;
		}

		public ContainerService.Builder CreateBuilder(ServiceName name, bool crearteNew, IObjectAccessor arguments)
		{
			var result = new ContainerService.Builder(name, this, crearteNew, arguments);
			if (!constructingServices.Add(name.Type))
			{
				var previous = GetTopBuilder();
				if (previous == null)
					throw new InvalidOperationException(string.Format("assertion failure, service [{0}]", name));
				var message = string.Format("cyclic dependency {0}{1} -> {0}",
					name.Type.FormatName(), previous.Type == name.Type ? "" : " ...-> " + previous.Type.FormatName());
				result.SetError(message);
				return result;
			}
			var pushedCount = 0;
			foreach (var newContract in name.Contracts)
			{
				foreach (var c in Contracts)
					if (newContract.EqualsIgnoringCase(c))
					{
						constructingServices.Remove(name.Type);
						Contracts.RemoveLast(pushedCount);
						const string messageFormat = "contract [{0}] already declared, all declared contracts [{1}]";
						var message = string.Format(messageFormat, newContract, InternalHelpers.FormatContractsKey(Contracts));
						result.SetError(message);
						return result;
					}
				Contracts.Add(newContract);
				pushedCount++;
			}
			var permutations = TryExpandUnions(Container.Configuration);
			result.ExpandedUnions = permutations == null
				? (ExpandedUnions?) null
				: new ExpandedUnions
				{
					permutations = permutations,
					poppedContracts = Contracts.PopMany(permutations.Length)
				};
			stack.Add(result);
			return result;
		}

		public ContainerService Build(ContainerService.Builder builder)
		{
			if (builder.ExpandedUnions != null)
				Contracts.AddRange(builder.ExpandedUnions.Value.poppedContracts);
			stack.RemoveLast();
			Contracts.RemoveLast(builder.SelfDeclaredContracts.Length);
			constructingServices.Remove(builder.Type);
			return builder.Build();
		}

		public string[][] TryExpandUnions(ConfigurationRegistry configuration)
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

		public ContainerService.Builder GetTopBuilder()
		{
			return stack.Count == 0 ? null : stack[stack.Count - 1];
		}

		public ContainerService.Builder GetPreviousBuilder()
		{
			return stack.Count <= 1 ? null : stack[stack.Count - 2];
		}
	}
}