using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SimpleContainer.Helpers
{
	public static class Utils
	{
		public static readonly Encoding utf8WithoutPreamble = new UTF8Encoding(false);

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

		public static string[] Split(this string s, string separator, StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries)
		{
			return s.Split(new[] { separator }, options);
		}

		public static string ReadUtf8String(this Stream stream)
		{
			return stream.ReadString(utf8WithoutPreamble);
		}

		public static string ReadString(this Stream stream, Encoding encoding)
		{
			return encoding.GetString(stream.ReadToEnd());
		}

		public static byte[] ReadToEnd(this Stream stream)
		{
			const int bufferSize = 1024;
			var buffer = new byte[bufferSize];
			var result = new MemoryStream();
			int size;
			do
			{
				size = stream.Read(buffer, 0, bufferSize);
				result.Write(buffer, 0, size);
			} while (size > 0);
			return result.ToArray();
		}
	}
}