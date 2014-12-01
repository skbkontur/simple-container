using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SimpleContainer.Tests.Helpers
{
	public static class TestHelpers
	{
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

		public static string JoinStrings<T>(this IEnumerable<T> source, string separator)
		{
			return source.Select(x => x.ToString()).JoinStrings(separator);
		}

		public static string JoinStrings(this IEnumerable<string> source, string separator)
		{
			return string.Join(separator, source.ToArray());
		}
	}
}