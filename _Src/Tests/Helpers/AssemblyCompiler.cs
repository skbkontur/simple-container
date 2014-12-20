using System;
using System.CodeDom.Compiler;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SimpleContainer.Hosting;

namespace SimpleContainer.Tests.Helpers
{
	public static class AssemblyCompiler
	{
		private static readonly Assembly[] defaultAssemblies =
		{
			Assembly.GetExecutingAssembly(),
			typeof (IComponent).Assembly,
			typeof (NameValueCollection).Assembly
		};

		static AssemblyCompiler()
		{
			CleanupTestAssemblies();
			AppDomain.CurrentDomain.DomainUnload += delegate { CleanupTestAssemblies(); };
		}

		public static Assembly Compile(string source, params Assembly[] references)
		{
			return Compile(source, null, references);
		}

		public static Assembly Compile(string source, string resultFileName, params Assembly[] references)
		{
			var compilationParameters = new CompilerParameters
			{
				OutputAssembly = resultFileName ?? "tmp_" + Guid.NewGuid().ToString("N") + ".dll",
				GenerateExecutable = false
			};
			foreach (var reference in references.Concat(defaultAssemblies).Select(x => x.GetName().Name + ".dll"))
				compilationParameters.ReferencedAssemblies.Add(reference);
			CompilerResults compilationResult;
			using (var codeDomProvider = CodeDomProvider.CreateProvider("C#"))
				compilationResult = codeDomProvider.CompileAssemblyFromSource(compilationParameters, source);
			if (compilationResult.Errors.HasErrors || compilationResult.Errors.HasWarnings)
			{
				var message = compilationResult.Errors
					.Cast<CompilerError>()
					.Select(x => string.Format("{0}:{1} {2}", x.Line, x.Column, x.ErrorText))
					.JoinStrings("\r\n");
				Assert.Fail(message);
			}
			return compilationResult.CompiledAssembly;
		}

		private static void CleanupTestAssemblies()
		{
			var testAssemblyFileNames = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory)
				.Where(x => Path.GetFileName(x).StartsWith("tmp_"));
			foreach (var fileName in testAssemblyFileNames)
				try
				{
					File.Delete(fileName);
				}
				catch (IOException)
				{
				}
		}
	}
}