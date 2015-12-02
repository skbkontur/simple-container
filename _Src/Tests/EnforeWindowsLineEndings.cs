using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SimpleContainer.Helpers;

namespace SimpleContainer.Tests
{
	[TestFixture]
	public class EnforeWindowsLineEndings
	{
		[Test]
		public void Test()
		{
			//#13#10(\r\n) - windows, #10(\n) - unix
			var srcDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\_Src");
			var sourceFiles = Directory.GetFiles(srcDirectory, "*.cs", SearchOption.AllDirectories);
			var invalidFiles = new List<string>();
			foreach (var f in sourceFiles)
			{
				var code = File.ReadAllText(f);
				for (var i = 0; i < code.Length; i++)
					if (code[i] == '\n' && (i == 0 || code[i - 1] != '\r'))
					{
						invalidFiles.Add(f);
						break;
					}
			}
			const string messageFormat = "the following files has unix style line endings #10 (\\n), " +
			                             "please fix it to windows style #13#10 (\\r\\n)\r\n{0}";
			Assert.That(invalidFiles.Count == 0,
				string.Format(messageFormat, invalidFiles.Select(x => "\t" + x).JoinStrings("\r\n")));
		}
	}
}