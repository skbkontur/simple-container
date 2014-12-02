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
		public readonly Stack<RequiredContract> requiredContracts = new Stack<RequiredContract>();
		public string ContractsKey { get; private set; }

		public struct RequiredContract
		{
			public string name;
			public ContractConfiguration configuration;
		}

		public ResolutionContext(IContainerConfiguration configuration, string contractName)
		{
			this.configuration = configuration;
			if (!string.IsNullOrEmpty(contractName))
				PushContract(contractName);
		}

		public T GetConfiguration<T>(Type type) where T : class
		{
			foreach (var requiredContract in requiredContracts)
			{
				var result = requiredContract.configuration.GetOrNull<T>(type);
				if (result == null)
					continue;
				current.Peek().service.usedContractName = requiredContract.name;
				return result;
			}
			return configuration.GetOrNull<T>(type);
		}

		public void Instantiate(string name, ContainerService containerService, SimpleContainer container)
		{
			var previous = current.Count == 0 ? null : current.Peek();
			var item = new ResolutionItem
			{
				depth = depth++,
				name = name,
				contractName = ContractsKey,
				contractDeclared = ContractsKey != null && previous != null &&
				                   (previous.contractName ?? "").Length < ContractsKey.Length,
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

		public IEnumerable<ContainerService> ResolveUsingContract(Type type, string name, string contractName,
			SimpleContainer container)
		{
			return (GetContractConfiguration(contractName).UnionContractNames ?? Enumerable.Repeat(contractName, 1))
				.Select(delegate(string c)
				{
					PushContract(c);
					var result = container.ResolveSingleton(type, name, this);
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
				if (state.contractName != null && state.service.usedContractName != null)
					writer.WriteUsedContract(state.contractName);
				if (state.contractName != null && state.contractDeclared)
				{
					writer.WriteMeta("->[");
					writer.WriteMeta(state.contractName);
					writer.WriteMeta("]");
				}
				if (state.service.instances.Count == 0)
				{
					writer.WriteMeta("!");
					if (state.message != null)
					{
						writer.WriteMeta(" - ");
						writer.WriteMeta(state.message);
					}
				}
				else if (state.service.instances.Count > 1)
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
			requiredContracts.Push(new RequiredContract
			{
				name = contractName,
				configuration = GetContractConfiguration(contractName)
			});
			ContractsKey += ContractsKey == null ? contractName : "->" + contractName;
		}

		private void PopContract()
		{
			var popped = requiredContracts.Pop();
			ContractsKey = ContractsKey.Length == popped.name.Length
				? null
				: ContractsKey.Substring(0, ContractsKey.Length - popped.name.Length - 2);
		}

		private class ResolutionItem
		{
			public int depth;
			public string name;
			public string message;
			public string contractName;
			public bool contractDeclared;
			public ContainerService service;
		}
	}
}