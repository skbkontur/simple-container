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
			return roots.Closure(x => x, children, int.MaxValue);
		}

		public static IEnumerable<TResult> Closure<T, TResult>(this IEnumerable<T> roots,
			Func<T, TResult> map,
			Func<TResult, IEnumerable<T>> children,
			int? depthLimit = null)
		{
			var seen = new HashSet<T>();
			var stack = new Stack<Tuple<T, int>>();
			foreach (var root in roots)
				stack.Push(new Tuple<T, int>(root, 1));
			while (stack.Count != 0)
			{
				var item = stack.Pop();

				var content = map(item.Item1);
				if (Equals(content, null))
					continue;
				if (seen.Contains(item.Item1))
					continue;
				seen.Add(item.Item1);
				yield return content;
				if (!depthLimit.HasValue || item.Item2 + 1 <= depthLimit.Value)
					foreach (var child in children(content))
						stack.Push(new Tuple<T, int>(child, item.Item2 + 1));
			}
		}

		public static string[] Split(this string s, string separator,
			StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries)
		{
			return s.Split(new[] {separator}, options);
		}
	}
}