using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation.Hacks;
using SimpleContainer.Infection;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class DependenciesInjector
	{
		private readonly IContainer container;

		private readonly NonConcurrentDictionary<ServiceName, Injection[]> injections =
			new NonConcurrentDictionary<ServiceName, Injection[]>();

		private static readonly MemberInjectionsProvider provider = new MemberInjectionsProvider();

		public DependenciesInjector(IContainer container)
		{
			this.container = container;
		}

		public BuiltUpService BuildUp(object target, IEnumerable<string> contracts)
		{
			var type = target.GetType();
			var name = new ServiceName(type, InternalHelpers.ToInternalContracts(contracts, type));
			var dependencies = GetInjections(name);
			foreach (var dependency in dependencies)
				dependency.setter(target, dependency.value.Single());
			return new BuiltUpService(dependencies);
		}

		public IEnumerable<Type> GetResolvedDependencies(ServiceName cacheKey)
		{
			return injections.ContainsKey(cacheKey)
				? provider.GetMembers(cacheKey.Type).Select(x => x.member.MemberType())
				: Enumerable.Empty<Type>();
		}

		public IEnumerable<Type> GetDependencies(Type type)
		{
			return provider.GetMembers(type).Select(x => x.member.MemberType());
		}

		private Injection[] GetInjections(ServiceName name)
		{
			return injections.GetOrAdd(name, DetectInjections);
		}

		private Injection[] DetectInjections(ServiceName cacheKey)
		{
			var memberSetters = provider.GetMembers(cacheKey.Type);
			var result = new Injection[memberSetters.Length];
			for (var i = 0; i < result.Length; i++)
			{
				var member = memberSetters[i].member;
				RequireContractAttribute requireContractAttribute;
				var contracts = member.TryGetCustomAttribute(out requireContractAttribute)
					? (IEnumerable<string>) new List<string>(cacheKey.Contracts) {requireContractAttribute.ContractName}
					: cacheKey.Contracts;
				try
				{
					result[i].value = container.Resolve(member.MemberType(), contracts);
					result[i].value.CheckSingleInstance();
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

		public struct Injection
		{
			public Action<object, object> setter;
			public ResolvedService value;
		}
	}
}