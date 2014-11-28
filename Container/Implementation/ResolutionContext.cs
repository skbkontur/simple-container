using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	public class ResolutionContext
	{
		private readonly IContainerConfiguration configuration;
		private readonly Stack<ResolutionItem> current = new Stack<ResolutionItem>();
		private readonly List<ResolutionItem> log = new List<ResolutionItem>();
		private readonly ISet<Type> currentTypes = new HashSet<Type>();
		private int depth;
		public string ContractName { get; private set; }
		private ContractConfiguration contractConfiguration;

		public ResolutionContext(IContainerConfiguration configuration, string contractName)
		{
			this.configuration = configuration;
			if (!string.IsNullOrEmpty(contractName))
				SetContract(contractName);
		}

		public T GetConfiguration<T>(Type type) where T : class
		{
			T result;
			if (contractConfiguration == null || (result = contractConfiguration.GetOrNull<T>(type)) == null)
				return configuration.GetOrNull<T>(type);
			current.Peek().service.contractUsed = true;
			return result;
		}

		public void Instantiate(string name, ContainerService containerService, SimpleContainer container)
		{
			var previous = current.Count == 0 ? null : current.Peek();
			var item = new ResolutionItem
			{
				depth = depth++,
				name = name,
				contractName = ContractName,
				contractDeclared = ContractName != null && previous != null && previous.contractName == null,
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
			if (ContractName != null)
				throw new SimpleContainerException(
					string.Format("nested contexts are not supported, outer contract [{0}], inner contract [{1}]\r\n{2}",
						ContractName, contractName, Format()));
			var result = (GetContractConfiguration(contractName).UnionContractNames ?? Enumerable.Repeat(contractName, 1))
				.Select(delegate(string c)
				{
					SetContract(c);
					return container.ResolveSingleton(type, name, this);
				})
				.ToArray();
			contractConfiguration = null;
			ContractName = null;
			return result;
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
				if (state.contractName != null && state.service.contractUsed)
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

		private void SetContract(string contractName)
		{
			contractConfiguration = GetContractConfiguration(contractName);
			ContractName = contractName;
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