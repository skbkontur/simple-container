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
		public static T Get<T>(this IContainer container)
		{
			return (T) container.Get(typeof (T), null);
		}

		public static T Create<T>(this IContainer container, string contract = null, object arguments = null)
		{
			return (T) container.Create(typeof (T), contract, arguments);
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
			container.DumpConstructionLog(type, contractName, entireResolutionContext, logWriter);
			return logWriter.GetText();
		}

		public static IEnumerable<T> GetInstanceCache<T>(this IContainer container)
		{
			return container.GetInstanceCache(typeof (T)).Cast<T>();
		}

		public static void Run(this IContainer container)
		{
			var runLogger = container.GetAll<IComponentLogger>().SingleOrDefault();
			foreach (var c in container.GetInstanceCache<IComponent>())
				using (runLogger != null ? runLogger.OnRunComponent(c.GetType()) : null)
					c.Run();
		}

		public static object Run(this IContainer container, Type type, string contract = null)
		{
			var result = container.Get(type, contract);
			container.Run();
			return result;
		}

		public static T Run<T>(this IContainer container, string contract = null)
		{
			return (T) container.Run(typeof (T), contract);
		}
	}
}