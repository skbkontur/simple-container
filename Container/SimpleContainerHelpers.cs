using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Reflection;

namespace SimpleContainer
{
	public static class SimpleContainerHelpers
	{
		public static T Get<T>(this IContainer container)
		{
			return (T) container.Get(typeof (T), null);
		}

		public static IEnumerable<Type> GetImplementationsOf<T>(this IContainer container)
		{
			return container.GetImplementationsOf(typeof (T));
		}

		public static T[] GetAll<T>(this IContainer container)
		{
			return container.GetAll(typeof (T)).Cast<T>().ToArray();
		}

		public static bool TryGet<T>(this IContainer container, out T result)
		{
			return container.GetAll<T>().TrySingle(out result);
		}

		public static IContainerConfiguration CreateContainerConfiguration(Type[] types,
			params Action<ContainerConfigurationBuilder>[] configureActions)
		{
			var builder = new ContainerConfigurationBuilder();
			foreach (var action in configureActions)
				action(builder);
			if (types != null)
				builder.ApplyScanners(types);
			return builder.Build();
		}

		public static IEnumerable<object> GetDependencyValues(this IContainer container, Type type)
		{
			return container.GetDependencies(type).SelectMany(container.GetAll);
		}

		public static IEnumerable<TDependency> GetDependencyValuesOfType<TDependency>(this IContainer container, Type type)
		{
			return container.GetDependencies(type)
				.Where(x => typeof (TDependency).IsAssignableFrom(x))
				.SelectMany(container.GetAll)
				.Cast<TDependency>();
		}

		public static IEnumerable<object> GetDependencyValuesRecursive(this IContainer container, Type type)
		{
			return container.GetDependenciesRecursive(type).SelectMany(container.GetAll);
		}

		public static IEnumerable<Type> GetDependenciesRecursive(this IContainer container, Type type)
		{
			return Utils.Closure(type, container.GetDependencies).Skip(1);
		}

		public static string GetConstructionLog(this IContainer container, Type type, string contractName = null,
			bool entireResolutionContext = false)
		{
			var logWriter = new SimpleTextLogWriter();
			container.DumpConstructionLog(type, contractName, entireResolutionContext, logWriter);
			return logWriter.GetText();
		}

		public static string DumpValue(object value)
		{
			if (value == null)
				return "[<null>]";
			var type = value.GetType();
			return ReflectionHelpers.simpleTypes.Contains(type)
				? string.Format("[{0}] of type [{1}]", value, type.FormatName())
				: string.Format("of type [{0}]", type.FormatName());
		}
	}
}