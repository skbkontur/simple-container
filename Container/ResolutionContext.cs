using System;
using System.Collections.Generic;
using SimpleContainer.Reflection;

namespace SimpleContainer
{
	public class ResolutionContext
	{
		private readonly IContainerConfiguration configuration;
		private readonly Stack<ResolutionItem> current = new Stack<ResolutionItem>();
		private readonly List<ResolutionItem> log = new List<ResolutionItem>();
		private readonly ISet<Type> currentTypes = new HashSet<Type>();
		private int depth;
		public string ContextKey { get; private set; }
		public object arguments;

		private IContainerConfiguration contextConfiguration;

		public ResolutionContext(IContainerConfiguration configuration, object arguments = null)
		{
			this.configuration = configuration;
			this.arguments = arguments;
		}

		public T GetConfiguration<T>(Type type) where T : class
		{
			T result;
			if (contextConfiguration == null || (result = contextConfiguration.GetOrNull<T>(type)) == null)
				return configuration.GetOrNull<T>(type);
			current.Peek().service.contractUsed = true;
			return result;
		}

		public void BeginResolve(string name, ContainerService service)
		{
			var previous = current.Count == 0 ? null : current.Peek();
			var item = new ResolutionItem
			{
				depth = depth++,
				name = name,
				contractName = ContextKey,
				contractDeclared = ContextKey != null && previous != null && previous.contractName == null,
				service = service
			};
			current.Push(item);
			log.Add(item);
			if (currentTypes.Contains(service.type))
				throw new SimpleContainerException(string.Format("cyclic dependency {0} ...-> {1} -> {0}\r\n{2}",
																 service.type.FormatName(), previous == null ? "null" : previous.service.type.FormatName(), Format()));
			currentTypes.Add(service.type);
			service.context = this;
		}

		public void EndResolve(ContainerService service)
		{
			currentTypes.Remove(current.Pop().service.type);
			depth--;
		}

		public bool ActivateContext(Type type, string parameterName)
		{
			var targetType = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
			var contextKey = targetType.Name + "." + parameterName;
			return ActivateContext(contextKey);
		}

		public bool ActivateContext(string contextKey)
		{
			if (string.IsNullOrEmpty(contextKey))
				return false;
			var newContextConfiguration = configuration.GetByKeyOrNull(contextKey);
			if (newContextConfiguration == null)
				return false;
			if (ContextKey != null)
				throw new SimpleContainerException(string.Format("nested contexts are not supported, outer context [{0}], inner context [{1}]\r\n{2}",
																 ContextKey, contextKey, Format()));
			ContextKey = contextKey;
			contextConfiguration = newContextConfiguration;
			return true;
		}

		public void DeactivateContext()
		{
			ContextKey = null;
			contextConfiguration = null;
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
				writer.WriteName(state.name != null && ReflectionHelpers.simpleTypes.Contains(state.service.type) ? state.name : state.service.type.FormatName());
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
