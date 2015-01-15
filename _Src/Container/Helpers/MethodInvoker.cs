using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace SimpleContainer.Helpers
{
	public static class MethodInvoker
	{
		private static readonly ConcurrentDictionary<MethodBase, Func<object, object[], object>> compiledMethods =
			new ConcurrentDictionary<MethodBase, Func<object, object[], object>>();

		private static readonly Func<MethodBase, Func<object, object[], object>> compileMethodDelegate =
			ReflectionHelpers.EmitCallOf;

		public static object Invoke(MethodBase method, object self, object[] actualArguments)
		{
			var factoryMethod = compiledMethods.GetOrAdd(method, compileMethodDelegate);
			return factoryMethod(self, actualArguments);
		}
	}
}