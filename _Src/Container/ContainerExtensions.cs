using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer
{
	public static class ContainerExtensions
	{
		public static T Get<T>(this IContainer container, string contract = null, bool dumpConstructionLog = false)
		{
			return (T) container.Get(typeof (T), contract, dumpConstructionLog);
		}

		public static object Get(this IContainer container, Type type, string contract = null, bool dumpConstructionLog = false)
		{
			return container.Get(type, string.IsNullOrEmpty(contract) ? new string[0] : new[] {contract}, dumpConstructionLog);
		}

		public static T Create<T>(this IContainer container, string contract = null, object arguments = null)
		{
			return (T) container.Create(typeof (T), string.IsNullOrEmpty(contract) ? new string[0] : new[] {contract}, arguments);
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
			return container.GetConstructionLog(type, contractName == null ? new string[0] : new[] { contractName }, entireResolutionContext);
		}

		public static string GetConstructionLog(this IContainer container, Type type, IEnumerable<string> contracts,
			bool entireResolutionContext = false)
		{
			var logWriter = new SimpleTextLogWriter();
			container.DumpConstructionLog(type, contracts, entireResolutionContext, logWriter);
			return logWriter.GetText();
		}
	}
}