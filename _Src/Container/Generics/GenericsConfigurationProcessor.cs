using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation.Hacks;

namespace SimpleContainer.Generics
{
	internal class GenericsConfigurationProcessor
	{
		private readonly Func<AssemblyName, bool> assemblyFilter;
		private readonly List<GenericComponent> configurators = new List<GenericComponent>();
		private readonly HashSet<Type> processedClosedTypes = new HashSet<Type>();
		private GenericDependency[] dependencies;
		private GenericComponent[] componentsWithConstraints;
		private readonly List<GenericOverrideInfo> genericOverrides = new List<GenericOverrideInfo>();
		private bool firstRunFinalized;

		public GenericsConfigurationProcessor(Func<AssemblyName, bool> assemblyFilter)
		{
			this.assemblyFilter = assemblyFilter;
		}

		private IEnumerable<Type> SelectGenericDependencies(Type definition)
		{
			ConstructorInfo constructor;
			if (!definition.GetConstructors().SafeTrySingle(out constructor))
				return Enumerable.Empty<Type>();
			return constructor
				.GetParameters()
				.Select(x => x.ParameterType.GetTypeInfo().IsGenericType && (x.ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
				                                               || x.ParameterType.GetGenericTypeDefinition() == typeof (Func<>))
					? x.ParameterType.GetGenericArguments()[0]
					: x.ParameterType)
				.Where(t => assemblyFilter(t.GetTypeInfo().Assembly.GetName()))
				.Where(t => t.GetTypeInfo().IsGenericType && t.GetTypeInfo().ContainsGenericParameters && TypeHelpers.HasEquivalentParameters(t, definition));
		}

		private Type[] GetGenericConstraintsOrNull(Type type)
		{
			var genericArguments = type.GetGenericArguments();
			if (genericArguments.Length != 1)
				return null;
			var constraints = genericArguments[0].GetTypeInfo().GetGenericParameterConstraints()
				.Where(c => assemblyFilter(c.GetTypeInfo().Assembly.GetName()))
				.ToArray();
			return constraints.Any() ? constraints : null;
		}

		private static bool IsGenericComponent(Type type)
		{
			return !type.GetTypeInfo().IsAbstract && type.GetTypeInfo().IsGenericType && !typeof(IEnumerable).IsAssignableFrom(type);
		}

		public void FirstRun(Type type)
		{
			CheckGenericOverrides(type);
			if (!IsGenericComponent(type))
				return;
			IEnumerable<Type> genericDependencies = SelectGenericDependencies(type).ToArray();
			if (genericDependencies.Any())
				configurators.Add(new GenericComponent(type, genericDependencies, null));
			else
			{
				var genericConstraint = GetGenericConstraintsOrNull(type);
				if (genericConstraint != null)
					configurators.Add(new GenericComponent(type, new Type[0], genericConstraint));
			}
		}

		private void CheckGenericOverrides(Type type)
		{
			if (!IsConcrete(type))
				return;
			var baseType = type.GetTypeInfo().BaseType;
			if (!IsGenericComponent(baseType))
				return;

			var genericArguments = baseType.GetGenericArguments();
			if (genericArguments.Length != 1)
				return;
			genericOverrides.Add(new GenericOverrideInfo
			{
				GenericType = baseType.GetGenericTypeDefinition(),
				TypeArgument = genericArguments[0]
			});
		}

		private static bool IsConcrete(Type type)
		{
			return !type.GetTypeInfo().IsAbstract && !type.GetTypeInfo().IsGenericType;
		}

		public void SecondRun(ContainerConfigurationBuilder builder, Type type)
		{
			if (!firstRunFinalized)
			{
				FinalizeFirstRun();
				firstRunFinalized = true;
			}
			if (!IsConcrete(type))
				return;

			CloseUsingDependencies(builder, type);
			CloseUsingConstraints(builder, type);
		}

		public void Reset()
		{
			processedClosedTypes.Clear();
		}

		private void CloseUsingDependencies(ContainerConfigurationBuilder builder, Type type)
		{
			var closedTypes = TypeHelpers.GetGenericInterfaces(type);
			foreach (var dependency in dependencies)
				foreach (var c in TypeHelpers.FindAllClosing(dependency.Type, closedTypes))
					dependency.Close(c, builder, processedClosedTypes);
		}

		private void FinalizeFirstRun()
		{
			foreach (var c1 in configurators)
				foreach (var c2 in configurators)
					if (!ReferenceEquals(c1, c2))
						c1.UseAsServiceProviderFor(c2);
			dependencies = configurators.SelectMany(x => x.GenericDependencies).ToArray();
			componentsWithConstraints = configurators.Where(x => x.GenericConstraint != null).ToArray();
			foreach (var genericOverrideInfo in genericOverrides)
			{
				var genericComponent = GetGenericComponentOrNull(genericOverrideInfo.GenericType);
				if (genericComponent != null)
					genericComponent.Overrides.Add(genericOverrideInfo);
			}
		}

		private GenericComponent GetGenericComponentOrNull(Type owner)
		{
			return configurators.FirstOrDefault(x => x.Owner == owner);
		}

		private void CloseUsingConstraints(ContainerConfigurationBuilder builder, Type type)
		{
			foreach (var component in componentsWithConstraints.Where(x => x.SatisfyConstraints(type)))
				component.Close(new[] {type}, builder, processedClosedTypes);
		}
	}
}