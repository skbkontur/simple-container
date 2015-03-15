using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class ResolutionContext : IContainerConfigurationRegistry
	{
		private readonly IContainerConfiguration configuration;
		private readonly List<ResolutionItem> current = new List<ResolutionItem>();
		private readonly List<ResolutionItem> log = new List<ResolutionItem>();
		private readonly ISet<Type> currentTypes = new HashSet<Type>();
		private int depth;
		private readonly List<ContractDeclaration> declaredContracts = new List<ContractDeclaration>();

		public ResolutionContext(IContainerConfiguration configuration, IEnumerable<string> contracts)
		{
			this.configuration = configuration;
			if (contracts == null)
				return;
			var contractsArray = contracts.ToArray();
			if (contractsArray.Length > 0)
				PushContractDeclarations(contractsArray);
		}

		public List<string> DeclaredContractNames()
		{
			return declaredContracts.Select(x => x.name).ToList();
		}

		public List<string> GetDeclaredContractsByNames(List<string> names)
		{
			return declaredContracts
				.Where(x => names.Contains(x.name, StringComparer.OrdinalIgnoreCase))
				.Select(x => x.name)
				.ToList();
		}

		public int DeclaredContractsCount()
		{
			return declaredContracts.Count;
		}

		public bool ContractDeclared(string name)
		{
			return declaredContracts.Any(x => x.name.EqualsIgnoringCase(name));
		}

		public string DeclaredContractsKey()
		{
			return InternalHelpers.FormatContractsKey(DeclaredContractNames());
		}

		public T GetOrNull<T>(Type type) where T : class
		{
			for (var i = declaredContracts.Count - 1; i >= 0; i--)
			{
				var declaration = declaredContracts[i];
				foreach (var definition in declaration.definitions)
				{
					var result = definition.GetOrNull<T>(type);
					if (result == null)
						continue;
					var containerService = GetTopService();
					containerService.UseContractWithName(declaration.name);
					foreach (var name in definition.RequiredContracts)
						containerService.UseContractWithName(name);
					return result;
				}
			}
			return configuration.GetOrNull<T>(type);
		}

		public void Instantiate(string name, ContainerService containerService, SimpleContainer container)
		{
			var previous = current.Count == 0 ? null : current[current.Count - 1];
			var declaredContacts = DeclaredContractsKey();
			var previousDeclaredContracts = previous == null ? "" : previous.declaredContacts ?? "";
			var item = new ResolutionItem
			{
				depth = depth++,
				name = name,
				declaredContacts = declaredContacts,
				contractDeclared = previousDeclaredContracts.Length < declaredContacts.Length,
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
			var item = new ResolutionItem
			{
				depth = depth,
				name = formalParameter.Name,
				declaredContacts = DeclaredContractsKey(),
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

		public ContainerService Resolve(Type type, List<string> contractNames, string name, SimpleContainer container)
		{
			var internalContracts = InternalHelpers.ToInternalContracts(contractNames, type);
			if (internalContracts == null)
				return container.ResolveSingleton(type, name, this);

			var unioned = internalContracts
				.Select(delegate(string s)
				{
					var configurations = configuration.GetContractConfigurations(s).EmptyIfNull().ToArray();
					return configurations.Length == 1 ? configurations[0].UnionContractNames : null;
				})
				.ToArray();
			if (unioned.All(x => x == null))
				return ResolveUsingContracts(type, name, container, internalContracts);
			var source = new List<List<string>>();
			for (var i = 0; i < internalContracts.Count; i++)
				source.Add(unioned[i] ?? new List<string>(1) {internalContracts[i]});
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
			PushContractDeclarations(contractNames);
			var result = container.ResolveSingleton(type, name, this);
			declaredContracts.RemoveRange(declaredContracts.Count - contractNames.Count, contractNames.Count);
			return result;
		}

		private void PushContractDeclarations(IEnumerable<string> contractNames)
		{
			foreach (var c in contractNames)
			{
				var duplicate = declaredContracts.FirstOrDefault(x => x.name.EqualsIgnoringCase(c));
				if (duplicate != null)
				{
					const string messageFormat = "contract [{0}] already declared, all declared contracts [{1}]\r\n{2}";
					throw new SimpleContainerException(string.Format(messageFormat, c, DeclaredContractsKey(), Format()));
				}
				var contractDeclaration = new ContractDeclaration
				{
					name = c,
					definitions = configuration.GetContractConfigurations(c)
						.EmptyIfNull()
						.Where(x => MatchWithDeclaredContracts(x.RequiredContracts))
						.ToList()
				};
				declaredContracts.Add(contractDeclaration);
			}
		}

		private bool MatchWithDeclaredContracts(List<string> required)
		{
			for (int r = 0, d = 0; r < required.Count; d++)
			{
				if (d >= declaredContracts.Count)
					return false;
				if (required[r].EqualsIgnoringCase(declaredContracts[d].name))
					r++;
			}
			return true;
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
				var isSimpleType = state.service.Type.IsSimpleType();
				var name = state.name != null && isSimpleType ? state.name : state.service.Type.FormatName();
				writer.WriteName(state.isStatic ? "(s)" + name : name);
				var usedContracts = state.service.GetUsedContractNames();
				if (usedContracts != null && usedContracts.Count > 0)
					writer.WriteUsedContract(InternalHelpers.FormatContractsKey(usedContracts));
				if (state.declaredContacts != null && state.contractDeclared)
				{
					writer.WriteMeta("->[");
					writer.WriteMeta(state.declaredContacts);
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

		private class ContractDeclaration
		{
			public string name;
			public List<ContractConfiguration> definitions;
		}

		private class ResolutionItem
		{
			public int depth;
			public string name;
			public string message;
			public string declaredContacts;
			public bool contractDeclared;
			public ContainerService service;
			public bool isStatic;
		}
	}
}