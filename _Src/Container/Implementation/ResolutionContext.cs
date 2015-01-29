using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class ResolutionContext
	{
		private readonly IContainerConfiguration configuration;
		private readonly List<ResolutionItem> current = new List<ResolutionItem>();
		private readonly List<ResolutionItem> log = new List<ResolutionItem>();
		private readonly ISet<Type> currentTypes = new HashSet<Type>();
		private int depth;
		public readonly List<RequiredContract> requiredContracts = new List<RequiredContract>();
		public object locker = new object();

		public ResolutionContext(IContainerConfiguration configuration, IEnumerable<string> contracts)
		{
			this.configuration = configuration;
			if (contracts == null)
				return;
			var contractsArray = contracts.ToArray();
			if (contractsArray.Length > 0)
				PushContracts(contractsArray);
		}

		public struct RequiredContract
		{
			public ContractConfiguration configuration;
			public List<int> requiredContractIndexes;
		}

		public List<string> RequiredContractNames()
		{
			return requiredContracts.Select(x => x.configuration.Name).Distinct().ToList();
		}

		public T GetConfiguration<T>(Type type) where T : class
		{
			for (var i = requiredContracts.Count - 1; i >= 0; i--)
			{
				var requiredContract = requiredContracts[i];
				var result = requiredContract.configuration.GetOrNull<T>(type);
				if (result == null)
					continue;
				var containerService = GetTopService();
				containerService.UseContractWithIndex(i);
				if (requiredContract.requiredContractIndexes != null)
					foreach (var index in requiredContract.requiredContractIndexes)
						containerService.UseContractWithIndex(index);
				return result;
			}
			return configuration.GetOrNull<T>(type);
		}

		public void Instantiate(string name, ContainerService containerService, SimpleContainer container)
		{
			var previous = current.Count == 0 ? null : current[current.Count - 1];
			var requiredContractNames = RequiredContractNames();
			var allContractsKey = InternalHelpers.FormatContractsKey(requiredContractNames);
			var previousContractsKey = previous == null ? "" : previous.allContactsKey ?? "";
			var item = new ResolutionItem
			{
				depth = depth++,
				name = name,
				allContactsKey = allContractsKey,
				contractDeclared = previousContractsKey.Length < allContractsKey.Length,
				service = containerService,
				isStatic = container.cacheLevel == CacheLevel.Static
			};
			current.Add(item);
			log.Add(item);
			if (!currentTypes.Add(containerService.Type))
				throw new SimpleContainerException(string.Format("cyclic dependency {0} ...-> {1} -> {0}\r\n{2}",
					containerService.Type.FormatName(), previous == null ? "null" : previous.service.Type.FormatName(), Format()));
			containerService.AttachToContext(this);
			container.Instantiate(containerService);
			current.RemoveAt(current.Count - 1);
			currentTypes.Remove(containerService.Type);
			depth--;
		}

		public void LogSimpleType(ParameterInfo formalParameter, object value, SimpleContainer container)
		{
			var containerService = new ContainerService(formalParameter.ParameterType);
			containerService.AddInstance(value);
			var requiredContractNames = RequiredContractNames();
			var allContractsKey = InternalHelpers.FormatContractsKey(requiredContractNames);
			var item = new ResolutionItem
			{
				depth = depth,
				name = formalParameter.Name,
				allContactsKey = allContractsKey,
				contractDeclared = false,
				service = containerService,
				isStatic = container.cacheLevel == CacheLevel.Static
			};
			log.Add(item);
		}

		public ContainerService GetTopService()
		{
			return current.Count == 0 ? null : current[current.Count - 1].service;
		}

		public ContainerService GetPreviousService()
		{
			return current.Count <= 1 ? null : current[current.Count - 2].service;
		}

		public ContainerService Resolve(Type type, string name, SimpleContainer container, List<string> contractNames)
		{
			if (contractNames == null)
				return container.ResolveSingleton(type, name, this);
			var unioned = contractNames
				.Select(delegate(string s)
				{
					var configurations = configuration.GetContractConfigurations(s).ToArray();
					return configurations.Length == 1 ? configurations[0].UnionContractNames : null;
				})
				.ToArray();
			if (unioned.All(x => x == null))
				return ResolveUsingContracts(type, name, container, contractNames);
			var source = new List<List<string>>();
			for (var i = 0; i < contractNames.Count; i++)
				source.Add(unioned[i] ?? new List<string>(1) {contractNames[i]});
			var result = new ContainerService(type);
			result.AttachToContext(this);
			foreach (var contracts in source.CartesianProduct())
			{
				var item = ResolveUsingContracts(type, name, container, contracts);
				result.UnionFrom(item);
			}
			result.EndResolveDependencies();
			return result;
		}

		public ContainerService ResolveUsingContracts(Type type, string name, SimpleContainer container,
			List<string> contractNames)
		{
			var pushedContractsCount = PushContracts(contractNames);
			var result = container.ResolveSingleton(type, name, this);
			requiredContracts.RemoveRange(requiredContracts.Count - pushedContractsCount, pushedContractsCount);
			return result;
		}

		private ContractConfiguration[] GetContractConfigurationsOrDefault(string s)
		{
			return configuration.GetContractConfigurations(s) ??
			       new[] {new ContractConfiguration(s, new List<string>(), new Dictionary<Type, object>(), null)};
		}

		private int PushContracts(IEnumerable<string> contractNames)
		{
			var pushedContractsCount = 0;
			foreach (var c in contractNames.SelectMany(GetContractConfigurationsOrDefault))
			{
				foreach (var requiredContract in requiredContracts)
				{
					var alreadyRequired = requiredContract.configuration.RequiredContracts.Count == 0 &&
					                      c.RequiredContracts.Count == 0 &&
					                      string.Equals(requiredContract.configuration.Name, c.Name, StringComparison.OrdinalIgnoreCase);
					if (alreadyRequired)
					{
						const string messageFormat = "contract [{0}] already required, all required contracts [{1}]\r\n{2}";
						throw new SimpleContainerException(string.Format(messageFormat,
							c.Name, InternalHelpers.FormatContractsKey(requiredContracts.Select(x => x.configuration.Name).ToList()), Format()));
					}
				}
				List<int> requiredIndexes;
				if (MatchedByRequiredContracts(c, out requiredIndexes))
				{
					requiredContracts.Add(new RequiredContract
					{
						configuration = c,
						requiredContractIndexes = requiredIndexes
					});
					pushedContractsCount++;
				}
			}
			return pushedContractsCount;
		}

		private bool MatchedByRequiredContracts(ContractConfiguration c, out List<int> requiredIndexes)
		{
			if (c.RequiredContracts.Count == 0)
			{
				requiredIndexes = null;
				return true;
			}
			requiredIndexes = new List<int>();
			var index = 0;
			foreach (var t in c.RequiredContracts)
			{
				if (!SearchForRequiredContract(ref index, t))
					return false;
				requiredIndexes.Add(index);
			}
			return true;
		}

		private bool SearchForRequiredContract(ref int index, string name)
		{
			while (index < requiredContracts.Count)
			{
				if (requiredContracts[index].configuration.Name == name)
					return true;
				index++;
			}
			return false;
		}

		public string Format()
		{
			var writer = new SimpleTextLogWriter();
			Format(null, writer);
			return writer.GetText();
		}

		public void Throw(string format, params object[] args)
		{
			throw new SimpleContainerException(string.Format(format, args) + "\r\n" + Format());
		}

		public void Comment(string message, params object[] args)
		{
			current[current.Count - 1].message = string.Format(message, args);
		}

		public void Format(ContainerService containerService, ISimpleLogWriter writer)
		{
			var startDepth = 0;
			var targetTypeFound = false;
			foreach (var state in log)
			{
				if (containerService != null && state.service != containerService && !targetTypeFound)
					continue;
				if (targetTypeFound && state.depth <= startDepth)
					break;
				if (!targetTypeFound)
				{
					targetTypeFound = true;
					startDepth = state.depth;
				}
				writer.WriteIndent(state.depth - startDepth);
				var isSimpleType = ReflectionHelpers.simpleTypes.Contains(state.service.Type);
				var name = state.name != null && isSimpleType ? state.name : state.service.Type.FormatName();
				writer.WriteName(state.isStatic ? "(s)" + name : name);
				var usedContracts = state.service.GetUsedContractNames();
				if (usedContracts != null && usedContracts.Count > 0)
					writer.WriteUsedContract(InternalHelpers.FormatContractsKey(usedContracts));
				if (state.allContactsKey != null && state.contractDeclared)
				{
					writer.WriteMeta("->[");
					writer.WriteMeta(state.allContactsKey);
					writer.WriteMeta("]");
				}
				if (state.service.Instances.Count == 0)
					writer.WriteMeta("!");
				else if (state.service.Instances.Count > 1)
					writer.WriteMeta("++");
				else if (isSimpleType)
					writer.WriteMeta(" -> " + (state.service.Instances[0] ?? "<null>"));
				if (state.message != null)
				{
					writer.WriteMeta(" - ");
					writer.WriteMeta(state.message);
				}
				writer.WriteNewLine();
			}
		}

		private class ResolutionItem
		{
			public int depth;
			public string name;
			public string message;
			public string allContactsKey;
			public bool contractDeclared;
			public ContainerService service;
			public bool isStatic;
		}
	}
}