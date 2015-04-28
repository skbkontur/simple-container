using System;
using System.Reflection;
using SimpleContainer.Implementation.Hacks;

namespace SimpleContainer.Helpers
{
	public static class MethodInvoker
	{
		private static readonly NonConcurrentDictionary<MethodBase, Func<object, object[], object>> compiledMethods =
			new NonConcurrentDictionary<MethodBase, Func<object, object[], object>>();

		private static readonly Func<MethodBase, Func<object, object[], object>> compileMethodDelegate = info => ((self, args)
			=>
		{
			if (info is ConstructorInfo)
				return ((ConstructorInfo) info).Invoke(args);
			return info.Invoke(self, args);
		});

		public static object Invoke(MethodBase method, object self, object[] actualArguments)
		{
			var factoryMethod = compiledMethods.GetOrAdd(method, compileMethodDelegate);
			return factoryMethod(self, actualArguments);
		}
	}
}