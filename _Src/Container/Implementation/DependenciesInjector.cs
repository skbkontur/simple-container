using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class DependenciesInjector
	{
		private readonly SimpleContainer container;

		private readonly ConcurrentDictionary<ServiceName, Injection[]> injections =
			new ConcurrentDictionary<ServiceName, Injection[]>();

		private static readonly MemberInjectionsProvider provider = new MemberInjectionsProvider();

		public DependenciesInjector(SimpleContainer container)
		{
			this.container = container;
		}

		public BuiltUpService BuildUp(ServiceName name, object target)
		{
			var dependencies = GetInjections(name);
			foreach (var dependency in dependencies)
				dependency.setter(target, dependency.value.Single());
			return new BuiltUpService(dependencies);
		}

		public IEnumerable<Type> GetResolvedDependencies(ServiceName name)
		{
			return injections.ContainsKey(name)
				? provider.GetMembers(name.Type).Select(x => x.member.MemberType())
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

		private Injection[] DetectInjections(ServiceName name)
		{
			var memberSetters = provider.GetMembers(name.Type);
			var result = new Injection[memberSetters.Length];
			for (var i = 0; i < result.Length; i++)
			{
				var member = memberSetters[i].member;
				try
				{
					result[i].value = container.Resolve(member.MemberType(),
						name.Contracts.ConcatIfNotNull(InternalHelpers.ParseContracts(member)));
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