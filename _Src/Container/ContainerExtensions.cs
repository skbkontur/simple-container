using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation;
using SimpleContainer.Interface;

namespace SimpleContainer
{
	public static class ContainerExtensions
	{
		public static T Get<T>(this IContainer container, string contract = null)
		{
			return (T) container.Get(typeof (T), contract);
		}

		public static ResolvedService<T> Resolve<T>(this IContainer container, params string[] contracts)
		{
			return new ResolvedService<T>(container.Resolve(typeof (T), contracts));
		}

		public static ResolvedService Resolve(this IContainer container, Type type, params string[] contracts)
		{
			return container.Resolve(type, contracts);
		}

		public static object Get(this IContainer container, Type type, string contract = null)
		{
			var contracts = string.IsNullOrEmpty(contract) ? InternalHelpers.emptyStrings : new[] {contract};
			var resolvedService = container.Resolve(type, contracts);
			if (!ResolutionContext.HasPendingResolutionContext)
				resolvedService.EnsureInitialized();
			return resolvedService.Single();
		}

		public static object Create(this IContainer container, Type type, string contract = null,
			object arguments = null)
		{
			var contracts = string.IsNullOrEmpty(contract) ? InternalHelpers.emptyStrings : new[] {contract};
			return container.Create(type, contracts, arguments);
		}

		public static T Create<T>(this IContainer container, string contract = null, object arguments = null)
		{
			var result = container.Create(typeof (T), contract, arguments);
			return (T) result;
		}

		public static IEnumerable<Type> GetImplementationsOf<T>(this IContainer container)
		{
			return container.GetImplementationsOf(typeof (T));
		}

		public static IEnumerable<object> GetAll(this IContainer container, Type type, params string[] contracts)
		{
			var resolvedService = container.Resolve(type, contracts);
			if (!ResolutionContext.HasPendingResolutionContext)
				resolvedService.EnsureInitialized();
			return resolvedService.All();
		}

		public static IEnumerable<T> GetAll<T>(this IContainer container, params string[] contracts)
		{
			var containerService = container.Resolve<T>(contracts);
			if (!ResolutionContext.HasPendingResolutionContext)
				containerService.EnsureInitialized();
			return containerService.All();
		}

		public static bool TryGet<T>(this IContainer container, out T result)
		{
			return container.GetAll<T>().TrySingle(out result);
		}

		public static IEnumerable<object> GetDependencyValues(this IContainer container, Type type)
		{
			return container.GetDependencies(type).SelectMany(t => container.GetAll(t));
		}

		public static IEnumerable<TDependency> GetDependencyValuesOfType<TDependency>(this IContainer container, Type type)
		{
			return container.GetDependencies(type)
				.Where(x => typeof (TDependency).IsAssignableFrom(x))
				.SelectMany(t => container.GetAll<TDependency>());
		}

		public static IEnumerable<object> GetDependencyValuesRecursive(this IContainer container, Type type)
		{
			return container.GetDependenciesRecursive(type).SelectMany(t => container.GetAll(t));
		}

		public static IEnumerable<Type> GetDependenciesRecursive(this IContainer container, Type type)
		{
			return Utils.Closure(type, container.GetDependencies).Skip(1);
		}
	}
}