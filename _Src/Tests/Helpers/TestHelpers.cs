using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SimpleContainer.Helpers;

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

		public static Type[] GetNestedTypesRecursive(this Type type, BindingFlags bindingFlags)
		{
			return type.GetNestedTypes(bindingFlags)
				.SelectMany(t => t.GetNestedTypesRecursive(bindingFlags).Prepend(t))
				.ToArray();
		}
	}
}