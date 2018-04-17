using System;
using System.Diagnostics;
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

		public static Assembly CompileAssembly(string source, params Assembly[] references)
		{
			return CompileAssembly(source, null, references);
		}

		public static Assembly CompileAssembly(string source, string resultFileName, params Assembly[] references)
		{
			var assemblyPath = CompileTo(source, resultFileName, references.Select(x => x.Location).ToArray());
			return Assembly.LoadFrom(assemblyPath);
		}

		public static string Compile(string source, params string[] references)
		{
			return CompileTo(source, null, references);
		}

		public static string CompileTo(string source, string resultFileName, params string[] referencesLocations)
		{
			var assemblyName = "tmp_" + Guid.NewGuid().ToString("N");
			var assemblyPath = resultFileName ?? assemblyName + ".dll";
			var syntaxTree = CSharpSyntaxTree.ParseText(source, path: assemblyName);

			var metadataReferences = referencesLocations
				.Concat(defaultAssemblies.Select(r => r.Location))
				.Select(r => MetadataReference.CreateFromFile(r));
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
			return assemblyPath;
		}


		public static void CleanupTestAssemblies()
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