using System.Collections.Generic;

namespace SimpleContainer.Helpers
{
	public static class DictionaryExtensions
	{
		public static TValue GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> source, TKey key,
			TValue defaultValue = default(TValue))
		{
			TValue result;
			return source.TryGetValue(key, out result) ? result : defaultValue;
		}
	}
}