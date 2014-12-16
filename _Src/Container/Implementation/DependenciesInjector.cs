using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Helpers;
using SimpleContainer.Helpers.ReflectionEmit;
using SimpleContainer.Infection;

namespace SimpleContainer.Implementation
{
	internal class DependenciesInjector
	{
		private readonly IContainer container;
		private readonly ConcurrentDictionary<Type, Injection[]> injections = new ConcurrentDictionary<Type, Injection[]>();

		private const BindingFlags bindingFlags =
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

		public DependenciesInjector(IContainer container)
		{
			this.container = container;
		}

		public void BuildUp(object target)
		{
			var dependencies = GetInjections(target.GetType());
			foreach (var dependency in dependencies)
				dependency.accessor.Set(target, dependency.value);
		}

		public IEnumerable<Type> GetDependencies(Type type)
		{
			return GetInjections(type).Select(x => x.accessor.MemberType);
		}

		private Injection[] GetInjections(Type type)
		{
			return injections.GetOrAdd(type, DetectInjections);
		}

		private Injection[] DetectInjections(Type type)
		{
			var selfInjections = type
				.GetProperties(bindingFlags)
				.Where(m => m.CanWrite)
				.Union(type.GetFields(bindingFlags).Cast<MemberInfo>())
				.Where(m => m.IsDefined(typeof (InjectAttribute), true))
				.ToArray();
			Injection[] baseInjections = null;
			if (!type.IsDefined<FrameworkBoundaryAttribute>(false))
			{
				var baseType = type.BaseType;
				if (baseType != typeof (object))
					baseInjections = GetInjections(baseType);
			}
			var baseInjectionsCount = (baseInjections == null ? 0 : baseInjections.Length);
			var result = new Injection[selfInjections.Length + baseInjectionsCount];
			if (baseInjectionsCount > 0)
				Array.Copy(baseInjections, 0, result, 0, baseInjectionsCount);
			for (var i = 0; i < selfInjections.Length; i++)
			{
				var member = selfInjections[i];
				var resultIndex = i + baseInjectionsCount;
				RequireContractAttribute requireContractAttribute;
				var contractName = member.TryGetCustomAttribute(out requireContractAttribute)
					? requireContractAttribute.ContractName
					: null;
				result[resultIndex].value = container.Get(member.MemberType(), contractName);
				result[resultIndex].accessor = MemberAccessor<object>.Get(member);
			}
			return result;
		}

		private struct Injection
		{
			public MemberAccessor<object> accessor;
			public object value;
		}
	}
}