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

		public string[] RequiredContractNames()
		{
			return requiredContracts.Select(x => x.name).ToArray();
		}

		public struct RequiredContract
		{
			public string name;
			public ContractConfiguration configuration;
		}

		public ResolutionContext(IContainerConfiguration configuration, IEnumerable<string> contractNames)
		{
			this.configuration = configuration;
			foreach (var c in contractNames)
				PushContract(c);
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

		public void Mark(ISet<ContainerService> marked)
		{
			var index = 0;
			Mark(ref index, marked);
		}

		private void Mark(ref int index, ISet<ContainerService> marked)
		{
			lock (locker)
			{
				var start = index;
				var item = log[start];
				var skip = item.service.Instances.Count == 0;
				if (!skip)
					marked.Add(item.service);
				index++;
				while (index < log.Count && log[index].depth > item.depth)
					if (skip)
						index++;
					else
						Mark(ref index, marked);
			}
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

		public IEnumerable<ContainerService> ResolveUsingContract(Type type, string name,
			string dependencyContract, string serviceContract, SimpleContainer container)
		{
			var dependencyContracts = dependencyContract == null
				? new string[] {null}
				: GetContractConfiguration(dependencyContract).UnionContractNames ?? Enumerable.Repeat(dependencyContract, 1);
			return dependencyContracts
				.Select(delegate(string c)
				{
					if (c != null)
						PushContract(c);
					if (serviceContract != null)
						PushContract(serviceContract);
					var result = container.ResolveSingleton(type, name, this);
					if (serviceContract != null)
						PopContract();
					if (c != null)
						PopContract();
					return result;
				})
				.ToArray();
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
				if (usedContracts != null && usedContracts.Length > 0)
					writer.WriteUsedContract(InternalHelpers.FormatContractsKey(usedContracts));
				if (state.allContactsKey != null && state.contractDeclared)
				{
					writer.WriteMeta("->[");
					writer.WriteMeta(state.allContactsKey);
					writer.WriteMeta("]");
				}
				if (state.service.Instances.Count == 0)
				{
					writer.WriteMeta("!");
					if (state.message != null)
					{
						writer.WriteMeta(" - ");
						writer.WriteMeta(state.message);
					}
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

		private void PushContract(string contractName)
		{
			foreach (var requiredContract in requiredContracts)
			{
				if (string.Equals(requiredContract.name, contractName, StringComparison.OrdinalIgnoreCase))
				{
					const string messageFormat = "contract [{0}] already required, all required contracts [{1}]\r\n{2}";
					throw new SimpleContainerException(string.Format(messageFormat,
						contractName, InternalHelpers.FormatContractsKey(requiredContracts.Select(x => x.name)), Format()));
				}
			}
			requiredContracts.Add(new RequiredContract
			{
				name = contractName,
				configuration = GetContractConfiguration(contractName)
			});
		}

		private void PopContract()
		{
			requiredContracts.RemoveAt(requiredContracts.Count - 1);
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