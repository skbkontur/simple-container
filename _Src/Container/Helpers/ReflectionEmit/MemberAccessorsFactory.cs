using System;
using System.Collections.Concurrent;
using System.Reflection;
using SimpleContainer.Implementation.Hacks;

namespace SimpleContainer.Helpers.ReflectionEmit
{
	internal static class MemberAccessorsFactory
	{
		private static readonly NonConcurrentDictionary<MemberInfo, Func<object, object>> getters =
			new NonConcurrentDictionary<MemberInfo, Func<object, object>>();

		private static readonly NonConcurrentDictionary<MemberInfo, Action<object, object>> setters =
			new NonConcurrentDictionary<MemberInfo, Action<object, object>>();

		private static readonly Func<MemberInfo, Func<object, object>> createGetter;
		private static readonly Func<MemberInfo, Action<object, object>> createSetter;

		static MemberAccessorsFactory()
		{
			createGetter = info => self =>
			{
				var fieldInfo = info as FieldInfo;
				if (fieldInfo != null)
					return fieldInfo.GetValue(self);
				return ((PropertyInfo) info).GetMethod.Invoke(self, new object[0]);
			};
			createSetter = info => ((self, value) =>
			{
				var fieldInfo = info as FieldInfo;
				if (fieldInfo != null)
					fieldInfo.SetValue(self, value);
				else
					((PropertyInfo) info).SetMethod.Invoke(self, new[] {value});
			});
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