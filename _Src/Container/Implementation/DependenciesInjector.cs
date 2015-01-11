using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class DependenciesInjector
	{
		private readonly IContainer container;

		private readonly ConcurrentDictionary<CacheKey, Injection[]> injections =
			new ConcurrentDictionary<CacheKey, Injection[]>();

		private static readonly MemberInjectionsProvider provider = new MemberInjectionsProvider();

		public DependenciesInjector(IContainer container)
		{
			this.container = container;
		}

		public void BuildUp(object target, IEnumerable<string> contracts)
		{
			var type = target.GetType();
			var cacheKey = new CacheKey(type, InternalHelpers.ToInternalContracts(contracts, type));
			var dependencies = GetInjections(cacheKey);
			foreach (var dependency in dependencies)
				dependency.setter(target, dependency.value);
		}

		public IEnumerable<Type> GetResolvedDependencies(CacheKey cacheKey)
		{
			return injections.ContainsKey(cacheKey)
				? provider.GetMembers(cacheKey.type).Select(x => x.member.MemberType())
				: Enumerable.Empty<Type>();
		}

		public IEnumerable<Type> GetDependencies(Type type)
		{
			return provider.GetMembers(type).Select(x => x.member.MemberType());
		}

		private Injection[] GetInjections(CacheKey cacheKey)
		{
			return injections.GetOrAdd(cacheKey, DetectInjections);
		}

		private Injection[] DetectInjections(CacheKey cacheKey)
		{
			var memberSetters = provider.GetMembers(cacheKey.type);
			var result = new Injection[memberSetters.Length];
			for (var i = 0; i < result.Length; i++)
			{
				var member = memberSetters[i].member;
				RequireContractAttribute requireContractAttribute;
				var contracts = member.TryGetCustomAttribute(out requireContractAttribute)
					? new List<string>(cacheKey.contracts) {requireContractAttribute.ContractName}
					: cacheKey.contracts;
				try
				{
					result[i].value = container.Get(member.MemberType(), contracts);
				}
				catch (SimpleContainerException e)
				{
					const string messageFormat = "can't resolve member [{0}.{1}]";
					throw new SimpleContainerException(string.Format(messageFormat, member.DeclaringType.FormatName(), member.Name), e);
				}
				result[i].setter = memberSetters[i].setter;
			}
			return result;
		}

		private struct Injection
		{
			public Action<object, object> setter;
			public object value;
		}
	}
}