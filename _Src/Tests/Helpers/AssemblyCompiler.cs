using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using SimpleContainer.Interface;
using SimpleContainer.Helpers;

namespace SimpleContainer.Tests.Helpers
{
	public static class AssemblyCompiler
	{
		private static readonly Assembly[] defaultAssemblies =
		{
			Assembly.GetExecutingAssembly(),
			typeof (IInitializable).Assembly,
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
			var syntaxTree = CSharpSyntaxTree.ParseText(source);
			var assemblyName = "tmp_" + Guid.NewGuid().ToString("N");
			var assemblyPath = resultFileName ?? assemblyName + ".dll";

			var metadataReferences = references
				.Select(r => MetadataReference.CreateFromFile(r.Location))
				.Concat(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
			var defaultNamespaces = new[] { "System" };
			var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
				.WithUsings(defaultNamespaces);
			
			var compilation = CSharpCompilation.Create(
				assemblyName,
				new[] { syntaxTree },
				metadataReferences,
				compilationOptions);
			
			using (var dllStream = File.OpenWrite(assemblyPath))
			{
				var emitResult = compilation.Emit(dllStream);
				if (!emitResult.Success)
				{
					var message = emitResult.Diagnostics
						.Select(d => $"{d.Location}: {d.Severity} {d.Id}: {d.GetMessage()}")
						.JoinStrings("\r\n");
					Assert.Fail(message);
				}
			}
			return Assembly.LoadFrom(assemblyPath);
		}

		private static void CleanupTestAssemblies()
		{
			var testAssemblyFileNames = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory)
				.Where(x => Path.GetFileName(x).StartsWith("tmp"));
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