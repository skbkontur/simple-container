using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using SimpleContainer.Helpers;
using SimpleContainer.Helpers.ReflectionEmit;
using SimpleContainer.Infection;

namespace SimpleContainer.Implementation
{
	internal class MemberInjectionsModel
	{
		private readonly ConcurrentDictionary<Type, InjectionMember[]> injections =
			new ConcurrentDictionary<Type, InjectionMember[]>();

		private const BindingFlags bindingFlags =
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

		private readonly Func<Type, InjectionMember[]> detectMembersForInjectDelegate;

		public MemberInjectionsModel()
		{
			detectMembersForInjectDelegate = DetectMembersForInject;
		}

		public InjectionMember[] GetMembers(Type type)
		{
			return injections.GetOrAdd(type, detectMembersForInjectDelegate);
		}

		private InjectionMember[] DetectMembersForInject(Type type)
		{
			var selfInjections = type
				.GetProperties(bindingFlags)
				.Where(m => m.CanWrite)
				.Union(type.GetFields(bindingFlags).Cast<MemberInfo>())
				.Where(m => m.IsDefined(typeof (InjectAttribute), true))
				.ToArray();
			InjectionMember[] baseInjections = null;
			if (!type.IsDefined<FrameworkBoundaryAttribute>(false))
			{
				var baseType = type.BaseType;
				if (baseType != typeof (object))
					baseInjections = GetMembers(baseType);
			}
			var baseInjectionsCount = (baseInjections == null ? 0 : baseInjections.Length);
			var result = new InjectionMember[selfInjections.Length + baseInjectionsCount];
			if (baseInjectionsCount > 0)
				Array.Copy(baseInjections, 0, result, 0, baseInjectionsCount);
			for (var i = 0; i < selfInjections.Length; i++)
			{
				var member = selfInjections[i];
				var resultIndex = i + baseInjectionsCount;
				result[resultIndex].setter = AccessorsFactory.GetSetter(member);
				result[resultIndex].member = member;
			}
			return result;
		}
	}
}