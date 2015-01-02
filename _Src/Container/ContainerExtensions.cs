using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Hosting;
using SimpleContainer.Implementation;

namespace SimpleContainer
{
	public static class ContainerExtensions
	{
		public static T Get<T>(this IContainer container, string contract = null)
		{
			return (T) container.Get(typeof (T), contract);
		}

		public static object Get(this IContainer container, Type type, string contract = null)
		{
			return container.Get(type, contract == null ? new string[0] : new[] {contract});
		}

		public static T Create<T>(this IContainer container, string contract = null, object arguments = null)
		{
			return (T) container.Create(typeof (T), contract == null ? new string[0] : new[] {contract}, arguments);
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
			var logWriter = new SimpleTextLogWriter();
			container.DumpConstructionLog(type, contractName == null ? new string[0] : new[] {contractName},
				entireResolutionContext, logWriter);
			return logWriter.GetText();
		}

		public static IEnumerable<ServiceInstance<T>> GetClosure<T>(this IContainer container, Type type,
			IEnumerable<string> contracts)
		{
			return container.GetClosure(type, contracts)
				.Where(x => x.Instance is T)
				.Select(x => x.Cast<T>());
		}

		public static object Run(this IContainer container, Type type, params string[] contracts)
		{
			var result = container.Get(type, contracts);
			RunComponents(container, type, contracts);
			return result;
		}

		public static T Run<T>(this IContainer container, params string[] contracts)
		{
			return (T) container.Run(typeof (T), contracts);
		}

		public static void RunComponents(this IContainer container, Type type, params string[] contracts)
		{
			var logger = container.GetAll<IComponentLogger>().SingleOrDefault();
			if (logger != null)
			{
				var constructionLog = container.GetConstructionLog(type, InternalHelpers.FormatContractsKey(contracts), true);
				logger.DumpConstructionLog(constructionLog);
			}
			foreach (var c in container.GetClosure<IComponent>(type, contracts))
				using (logger != null ? logger.OnRunComponent(c) : null)
					c.Instance.Run();
		}
	}
}