using System;
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
			typeof (object).Assembly
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
			var assemblyName = "tmp_" + Guid.NewGuid().ToString("N");
			var assemblyPath = resultFileName ?? assemblyName + ".dll";
			var syntaxTree = CSharpSyntaxTree.ParseText(source, path: assemblyName);

			var metadataReferences = references
				.Select(r => MetadataReference.CreateFromFile(r.Location))
				.Concat(defaultAssemblies.Select(r => MetadataReference.CreateFromFile(r.Location)));
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
						.JoinStrings(Environment.NewLine);
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