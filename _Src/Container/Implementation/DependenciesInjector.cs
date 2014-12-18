using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;

namespace SimpleContainer.Implementation
{
	internal class DependenciesInjector
	{
		private readonly IContainer container;

		private readonly ConcurrentDictionary<CacheKey, Injection[]> injections =
			new ConcurrentDictionary<CacheKey, Injection[]>();

		private static readonly MemberInjectionsModel model = new MemberInjectionsModel();

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
				? model.GetMembers(cacheKey.type).Select(x => x.member.MemberType())
				: Enumerable.Empty<Type>();
		}

		public IEnumerable<Type> GetDependencies(Type type)
		{
			return model.GetMembers(type).Select(x => x.member.MemberType());
		}

		private Injection[] GetInjections(CacheKey cacheKey)
		{
			return injections.GetOrAdd(cacheKey, DetectInjections);
		}

		private Injection[] DetectInjections(CacheKey cacheKey)
		{
			var memberAccessors = model.GetMembers(cacheKey.type);
			var result = new Injection[memberAccessors.Length];
			for (var i = 0; i < result.Length; i++)
			{
				RequireContractAttribute requireContractAttribute;
				var member = memberAccessors[i].member;
				var contracts = member.TryGetCustomAttribute(out requireContractAttribute)
					? new List<string>(cacheKey.contracts) {requireContractAttribute.ContractName}
					: cacheKey.contracts;
				result[i].value = container.Get(member.MemberType(), contracts);
				result[i].setter = memberAccessors[i].setter;
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