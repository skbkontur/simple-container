using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;

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
			if (contracts != null)
				PushContracts(contracts);
		}

		public List<string> RequiredContractNames()
		{
			return requiredContracts.Select(x => x.name).ToList();
		}

		public struct RequiredContract
		{
			public string name;
			public ContractConfiguration configuration;
		}

		public T GetConfiguration<T>(Type type) where T : class
		{
			for (var i = requiredContracts.Count - 1; i >= 0; i--)
			{
				var requiredContract = requiredContracts[i];
				var result = requiredContract.configuration.GetOrNull<T>(type);
				if (result == null)
					continue;
				GetTopService().UseContractWithIndex(i);
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
			var unioned = contractNames.Select(c => GetContractConfiguration(c).UnionContractNames).ToArray();
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
			return result;
		}

		public ContainerService ResolveUsingContracts(Type type, string name, SimpleContainer container,
			List<string> contractNames)
		{
			PushContracts(contractNames);
			var result = container.ResolveSingleton(type, name, this);
			for (var i = 0; i < contractNames.Count; i++)
				requiredContracts.RemoveAt(requiredContracts.Count - 1);
			return result;
		}

		private void PushContracts(IEnumerable<string> contractNames)
		{
			foreach (var c in contractNames)
			{
				foreach (var requiredContract in requiredContracts)
				{
					if (!string.Equals(requiredContract.name, c, StringComparison.OrdinalIgnoreCase))
						continue;
					const string messageFormat = "contract [{0}] already required, all required contracts [{1}]\r\n{2}";
					throw new SimpleContainerException(string.Format(messageFormat,
						c, InternalHelpers.FormatContractsKey(requiredContracts.Select(x => x.name)), Format()));
				}
				requiredContracts.Add(new RequiredContract
				{
					name = c,
					configuration = GetContractConfiguration(c)
				});
			}
		}

		public string Format()
		{
			var writer = new SimpleTextLogWriter();
			Format(null, null, writer);
			return writer.GetText();
		}

		public void Throw(string format, params object[] args)
		{
			throw new SimpleContainerException(string.Format(format, args) + "\r\n" + Format());
		}

		public void Report(string message, params object[] args)
		{
			current[current.Count - 1].message = string.Format(message, args);
		}

		public void Format(Type targetType, string contractsKey, ISimpleLogWriter writer)
		{
			var startDepth = 0;
			var targetTypeFound = false;
			foreach (var state in log)
			{
				if (targetType != null &&
				    (state.service.Type != targetType || state.allContactsKey != contractsKey) &&
				    !targetTypeFound)
					continue;
				if (targetTypeFound && state.depth <= startDepth)
					break;
				if (targetType != null && !targetTypeFound)
				{
					targetTypeFound = true;
					startDepth = state.depth;
				}
				writer.WriteIndent(state.depth - startDepth);
				var name = state.name != null && ReflectionHelpers.simpleTypes.Contains(state.service.Type)
					? state.name
					: state.service.Type.FormatName();
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
				if (state.message != null)
				{
					writer.WriteMeta(" - ");
					writer.WriteMeta(state.message);
				}
				else if (state.service.Instances.Count > 1)
					writer.WriteMeta("++");
				writer.WriteNewLine();
			}
		}

		private ContractConfiguration GetContractConfiguration(string contractName)
		{
			var result = configuration.GetContractConfiguration(contractName);
			if (result == null)
				throw new SimpleContainerException(string.Format("contract [{0}] is not configured\r\n{1}", contractName, Format()));
			return result;
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