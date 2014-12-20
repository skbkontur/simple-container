using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace SimpleContainer.Helpers.ReflectionEmit
{
	public static class MemberAccessorsFactory
	{
		private static readonly ConcurrentDictionary<MemberInfo, Func<object, object>> getters =
			new ConcurrentDictionary<MemberInfo, Func<object, object>>();

		private static readonly ConcurrentDictionary<MemberInfo, Action<object, object>> setters =
			new ConcurrentDictionary<MemberInfo, Action<object, object>>();

		private static readonly Func<MemberInfo, Func<object, object>> createGetter;
		private static readonly Func<MemberInfo, Action<object, object>> createSetter;

		static MemberAccessorsFactory()
		{
			createGetter = info => MemberAccessorFactory<object>.Create(info).CreateGetter();
			createSetter = info => MemberAccessorFactory<object>.Create(info).CreateSetter();
		}

		public static Func<object, object> GetGetter(MemberInfo member)
		{
			return getters.GetOrAdd(member, createGetter);
		}

		public static Action<object, object> GetSetter(MemberInfo member)
		{
			return setters.GetOrAdd(member, createSetter);
		}
	}
}