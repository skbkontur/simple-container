using System;
using System.Collections.Generic;

namespace SimpleContainer.Helpers
{
	internal static class Helpers
	{
		public static bool EqualsIgnoringCase(this string s1, string s2)
		{
			return string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);
		}

		public static TValue GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> source, TKey key,
			TValue defaultValue = default(TValue))
		{
			TValue result;
			return source.TryGetValue(key, out result) ? result : defaultValue;
		}

		public static void RemoveLast<T>(this List<T> list, int count = 1)
		{
			list.RemoveRange(list.Count - count, count);
		}

		public static T[] PopMany<T>(this List<T> list, int count = 1)
		{
			var result = new T[count];
			list.CopyTo(list.Count - count, result, 0, count);
			list.RemoveLast(count);
			return result;
		}
	}
}