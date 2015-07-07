using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Generics;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation.Hacks;

namespace SimpleContainer.Implementation
{
	internal class GenericsAutoCloser
	{
		private readonly Dictionary<Type, List<Type>> hierarchy;
		private readonly Func<AssemblyName, bool> assemblyFilter;
		private readonly NonConcurrentDictionary<Type, Type[]> cache = new NonConcurrentDictionary<Type, Type[]>();

		public GenericsAutoCloser(Dictionary<Type,List<Type>> hierarchy, Func<AssemblyName, bool> assemblyFilter)
		{
			this.hierarchy = hierarchy;
			this.assemblyFilter = assemblyFilter;
		}

		public Type[] AutoCloseDefinition(Type type)
		{
			Type[] result;
			return cache.TryGetValue(type, out result) ? result : InferGenerics(type);
		}

		private Type[] InferGenerics(Type type)
		{
			var context = new Dictionary<Type, GenericDefinition>();
			Mark(type, context);
			Deduce(context);
			Publish(context);
			return context[type].closures.ToArray();
		}

		private void Publish(Dictionary<Type, GenericDefinition> context)
		{
			foreach (var p in context)
				cache.TryAdd(p.Key, p.Value.closures.ToArray());
		}

		private static void Deduce(Dictionary<Type, GenericDefinition> context)
		{
			var targets = new Queue<GenericDefinition>();
			foreach (var p in context)
				if (p.Value.closures.Count > 0)
					targets.Enqueue(p.Value);
			while (targets.Count > 0)
			{
				var target = targets.Dequeue();
				foreach (var referer in target.referers)
				{
					var anyRefererClosed = false;
					foreach (var closure in target.closures)
					{
						if (referer.isInterface)
						{
							var closedReferers = closure.ImplementationsOf(referer.definition.type);
							foreach (var closedReferer in closedReferers)
								anyRefererClosed |= referer.definition.closures.Add(closedReferer);
						}
						else
						{
							var closedReferer = referer.definition.type.TryCloseByPattern(referer.pattern, closure);
							if (closedReferer != null)
								anyRefererClosed |= referer.definition.closures.Add(closedReferer);
						}
					}
					if (anyRefererClosed)
						targets.Enqueue(referer.definition);
				}
			}
		}

		private GenericDefinition Mark(Type definition, Dictionary<Type, GenericDefinition> context)
		{
			GenericDefinition result;
			if (context.TryGetValue(definition, out result))
				return result;
			result = new GenericDefinition {type = definition};
			context.Add(definition, result);
			Type[] types;
			if (cache.TryGetValue(definition, out types))
				foreach (var type in types)
					result.closures.Add(type);
			else if (definition.GetTypeInfo().IsAbstract)
				MarkInterface(result, context);
			else
				MarkImplementation(result, context);
			return result;
		}

		private void MarkImplementation(GenericDefinition definition, Dictionary<Type, GenericDefinition> context)
		{
			var ctor = definition.type.GetConstructor();
			if (!ctor.isOk)
				return;
			var parameters = ctor.value.GetParameters();
			var hasAnyGenericDependencies = false;
			foreach (var parameter in parameters)
			{
				var parameterType = parameter.ParameterType;
				if (parameterType.GetTypeInfo().IsGenericType && (parameterType.GetGenericTypeDefinition() == typeof (IEnumerable<>)
				                                    || parameterType.GetGenericTypeDefinition() == typeof (Func<>)))
					parameterType = parameterType.GetGenericArguments()[0];
				if (parameterType.IsSimpleType())
					continue;
				if (!assemblyFilter(parameterType.GetTypeInfo().Assembly.GetName()))
					continue;
				if (!parameterType.GetTypeInfo().IsGenericType)
					continue;
				if (!parameterType.GetTypeInfo().ContainsGenericParameters)
					continue;
				if (!TypeHelpers.HasEquivalentParameters(parameterType, definition.type))
					continue;
				hasAnyGenericDependencies = true;
				Mark(parameterType.GetGenericTypeDefinition(), context).referers.Add(new GenericReferer
				{
					definition = definition,
					pattern = parameterType
				});
			}
			if (!hasAnyGenericDependencies)
				MarkImplementationConstraints(definition);
		}

		private void MarkInterface(GenericDefinition definition, Dictionary<Type, GenericDefinition> context)
		{
			var implementationTypes = hierarchy.GetOrDefault(definition.type);
			if (implementationTypes == null)
				return;
			foreach (var implType in implementationTypes)
			{
				var interfaceImpls = implType.ImplementationsOf(definition.type);
				if (implType.GetTypeInfo().IsGenericType)
				{
					var markedImpl = Mark(implType, context);
					foreach (var interfaceImpl in interfaceImpls)
						markedImpl.referers.Add(new GenericReferer
						{
							definition = definition,
							pattern = interfaceImpl,
							isInterface = true
						});
				}
				else
					foreach (var interfaceImpl in interfaceImpls)
					{
						var closure = definition.type.TryCloseByPattern(definition.type, interfaceImpl);
						if (closure != null)
							definition.closures.Add(closure);
					}
			}
		}

		private void MarkImplementationConstraints(GenericDefinition definition)
		{
			var genericArguments = definition.type.GetGenericArguments();
			if (genericArguments.Length != 1)
				return;
			var constraints = genericArguments[0].GetTypeInfo().GetGenericParameterConstraints();
			if (constraints.Length == 0)
				return;
			foreach (var c in constraints)
				if (!assemblyFilter(c.GetTypeInfo().Assembly.GetName()))
					return;
			var impls = hierarchy.GetOrDefault(constraints[0]) ?? InternalHelpers.emptyTypesList;
			for (var i = 1; i < constraints.Length; i++)
			{
				if (impls.Count == 0)
					return;
				var current = hierarchy.GetOrDefault(constraints[i]) ?? InternalHelpers.emptyTypesList;
				for (var j = impls.Count - 1; j >= 0; j--)
					if (current.IndexOf(impls[j]) < 0)
						impls.RemoveAt(j);
			}
			if (impls.Count == 0)
				return;
			var nonGenericOverrides = (hierarchy.GetOrDefault(definition.type) ?? InternalHelpers.emptyTypesList)
				.Where(x => !x.GetTypeInfo().IsGenericType)
				.ToArray();
			foreach (var impl in impls)
			{
				if (genericArguments[0].GetTypeInfo().GenericParameterAttributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint))
					if (impl.GetConstructors().FirstOrDefault(x=>x.GetParameters().Length == 0) == null)
						continue;
				var closedItem = definition.type.MakeGenericType(impl);
				var overriden = false;
				foreach (var nonGenericOverride in nonGenericOverrides)
					if (closedItem.IsAssignableFrom(nonGenericOverride))
					{
						overriden = true;
						break;
					}
				if (!overriden)
					definition.closures.Add(closedItem);
			}
		}

		private class GenericDefinition
		{
			public readonly List<GenericReferer> referers = new List<GenericReferer>();
			public readonly HashSet<Type> closures = new HashSet<Type>();
			public Type type;
		}

		private class GenericReferer
		{
			public GenericDefinition definition;
			public Type pattern;
			public bool isInterface;
		}
	}
}