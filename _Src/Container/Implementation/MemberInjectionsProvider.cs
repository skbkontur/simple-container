using System;
using System.Linq;
using System.Reflection;
using SimpleContainer.Helpers;
using SimpleContainer.Helpers.ReflectionEmit;
using SimpleContainer.Implementation.Hacks;
using SimpleContainer.Infection;

namespace SimpleContainer.Implementation
{
	internal class MemberInjectionsProvider
	{
		private readonly NonConcurrentDictionary<Type, MemberSetter[]> injections =
			new NonConcurrentDictionary<Type, MemberSetter[]>();

		private const BindingFlags bindingFlags =
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

		private readonly Func<Type, MemberSetter[]> createMemberSettersDelegate;

		public MemberInjectionsProvider()
		{
			createMemberSettersDelegate = CreateMemberSetters;
		}

		public MemberSetter[] GetMembers(Type type)
		{
			return injections.GetOrAdd(type, createMemberSettersDelegate);
		}

		private MemberSetter[] CreateMemberSetters(Type type)
		{
			var selfMembers = type
				.GetProperties()
				.Where(m => m.CanWrite)
				.Union(type.GetFields().Cast<MemberInfo>())
				.Where(m => m.IsDefined(typeof (InjectAttribute), true))
				.ToArray();
			MemberSetter[] baseSetters = null;
			if (!type.IsDefined<FrameworkBoundaryAttribute>(false))
			{
				var baseType = type.GetTypeInfo().BaseType;
				if (baseType != typeof (object))
					baseSetters = GetMembers(baseType);
			}
			if (selfMembers.Length == 0 && baseSetters != null)
				return baseSetters;
			var baseMembersCount = baseSetters == null ? 0 : baseSetters.Length;
			var result = new MemberSetter[selfMembers.Length + baseMembersCount];
			if (baseMembersCount > 0)
				Array.Copy(baseSetters, 0, result, 0, baseMembersCount);
			for (var i = 0; i < selfMembers.Length; i++)
			{
				var member = selfMembers[i];
				var resultIndex = i + baseMembersCount;
				result[resultIndex].member = member;
				result[resultIndex].setter = MemberAccessorsFactory.GetSetter(member);
			}
			return result;
		}
	}
}