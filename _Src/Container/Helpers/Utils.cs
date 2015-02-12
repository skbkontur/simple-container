using System;
using System.Collections.Generic;

namespace SimpleContainer.Helpers
{
	internal static class Utils
	{
		public static List<List<T>> CartesianProduct<T>(this List<List<T>> source)
		{
			if (source.Count == 0)
				return source;
			var result = new List<List<T>>();
			if (source.Count == 1)
			{
				var item = source[0];
				foreach (var t in item)
					result.Add(new List<T>(1) {t});
				return result;
			}
			var currentResult = new T[source.Count];
			CartesianIteration(0, source, currentResult, result);
			return result;
		}

		private static void CartesianIteration<T>(int index, List<List<T>> source,
			T[] currentResult, List<List<T>> result)
		{
			var mySource = source[index];
			foreach (var t in mySource)
			{
				currentResult[index] = t;
				if (index < currentResult.Length - 1)
					CartesianIteration(index + 1, source, currentResult, result);
				else
					result.Add(new List<T>(currentResult));
			}
		}

		public static IEnumerable<T> Closure<T>(T root, Func<T, IEnumerable<T>> children)
		{
			return EnumerableHelpers.Return(root).Closure(children);
		}

		public static IEnumerable<T> Closure<T>(this IEnumerable<T> roots, Func<T, IEnumerable<T>> children)
		{
			return roots.Closure(x => x, (x, _) => children(x));
		}

		public static IEnumerable<TResult> Closure<T, TResult>(this IEnumerable<T> roots, Func<T, TResult> map,
			Func<T, TResult, IEnumerable<T>> children)
		{
			var seen = new HashSet<T>();
			var stack = new Stack<T>();
			foreach (var root in roots)
				stack.Push(root);
			while (stack.Count != 0)
			{
				var item = stack.Pop();
				var content = map(item);
				if (Equals(content, null))
					continue;
				if (seen.Contains(item))
					continue;
				seen.Add(item);
				yield return content;
				foreach (var child in children(item, content))
					stack.Push(child);
			}
		}
	}
}