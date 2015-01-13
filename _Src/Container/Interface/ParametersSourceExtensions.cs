using System;

namespace SimpleContainer.Interface
{
	public static class ParametersSourceExtensions
	{
		public static T Get<T>(this IParametersSource parameters, string name)
		{
			if (parameters == null)
				throw new InvalidOperationException("parameters is not set");
			object result;
			if (!parameters.TryGet(name, typeof (T), out result))
				throw new InvalidOperationException(string.Format("can't get parameter [{0}]", name));
			return (T) result;
		}

		public static T GetOrDefault<T>(this IParametersSource parameters, string name, T defaultValue = default (T))
		{
			T result;
			return parameters.TryGet(name, out result) ? result : defaultValue;
		}

		public static bool TryGet<T>(this IParametersSource parameters, string name, out T result)
		{
			object resultObject;
			if (parameters == null || !parameters.TryGet(name, typeof (T), out resultObject))
			{
				result = default (T);
				return false;
			}
			result = (T) resultObject;
			return true;
		}
	}
}