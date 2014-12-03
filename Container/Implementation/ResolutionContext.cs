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
		private readonly Stack<ResolutionItem> current = new Stack<ResolutionItem>();
		private readonly List<ResolutionItem> log = new List<ResolutionItem>();
		private readonly ISet<Type> currentTypes = new HashSet<Type>();
		private int depth;
		public readonly List<RequiredContract> requiredContracts = new List<RequiredContract>();

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
				current.Peek().service.UseContractWithIndex(i);
				return result;
			}
			return configuration.GetOrNull<T>(type);
		}

		public T GetInitialContainerConfiguration<T>(Type type) where T : class
		{
			return configuration.GetOrNull<T>(type);
		}

		public void Instantiate(string name, ContainerService containerService, SimpleContainer container)
		{
			var previous = current.Count == 0 ? null : current.Peek();
			var requiredContractNames = RequiredContractNames();
			var allContractsKey = InternalHelpers.FormatContractsKey(requiredContractNames);
			var item = new ResolutionItem
			{
				depth = depth++,
				name = name,
				allContactsKey = allContractsKey,
				contractDeclared = requiredContractNames.Length > 0 && previous != null &&
				                   (previous.allContactsKey ?? "").Length < allContractsKey.Length,
				service = containerService
			};
			current.Push(item);
			log.Add(item);
			if (currentTypes.Contains(containerService.type))
				throw new SimpleContainerException(string.Format("cyclic dependency {0} ...-> {1} -> {0}\r\n{2}",
					containerService.type.FormatName(), previous == null ? "null" : previous.service.type.FormatName(), Format()));
			currentTypes.Add(containerService.type);
			containerService.context = this;
			container.Instantiate(containerService);
			currentTypes.Remove(current.Pop().service.type);
			depth--;
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

		public string Format(Type targetType = null)
		{
			var writer = new SimpleTextLogWriter();
			Format(targetType, writer);
			return writer.GetText();
		}

		public void Throw(string format, params object[] args)
		{
			throw new SimpleContainerException(string.Format(format, args) + "\r\n" + Format());
		}

		public void Report(string message, params object[] args)
		{
			current.Peek().message = string.Format(message, args);
		}

		public void Format(Type targetType, ISimpleLogWriter writer)
		{
			var startDepth = 0;
			var targetTypeFound = false;
			foreach (var state in log)
			{
				if (targetType != null && state.service.type != targetType && !targetTypeFound)
					continue;
				if (targetTypeFound && state.depth <= startDepth)
					break;
				if (targetType != null && !targetTypeFound)
				{
					targetTypeFound = true;
					startDepth = state.depth;
				}
				writer.WriteIndent(state.depth - startDepth);
				writer.WriteName(state.name != null && ReflectionHelpers.simpleTypes.Contains(state.service.type)
					? state.name
					: state.service.type.FormatName());
				var usedContractNames = state.service.GetUsedContractNames();
				if (usedContractNames.Length > 0)
					writer.WriteUsedContract(InternalHelpers.FormatContractsKey(usedContractNames));
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
		}
	}
}