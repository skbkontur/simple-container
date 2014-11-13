using System;
using System.CodeDom.Compiler;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SimpleContainer.Helpers;
using SimpleContainer.Hosting;

namespace SimpleContainer.Tests.Hosting
{
	public abstract class HostingTest : UnitTestBase
	{
		public class Simple : HostingTest
		{
			private const string a1Code = @"
					using System;
					using System.Collections.Specialized;
					using SimpleContainer.Hosting;

					namespace A1
					{
						public class A1Runnable: IRunnable
						{
							public void Run(NameValueCollection arguments)
							{
							}
						}
					}
				";

			private const string a2Code = @"
					using System;
					using System.Collections.Specialized;
					using SimpleContainer.Hosting;

					namespace A2
					{
						public class A2Runnable: IRunnable
						{
							public void Run(NameValueCollection arguments)
							{
							}
						}
					}
				";

			[Test]
			public void Test()
			{
				var a1 = CompileAssembly(a1Code, typeof(IRunnable).Assembly, typeof(NameValueCollection).Assembly);
				var a2 = CompileAssembly(a2Code, typeof(IRunnable).Assembly, typeof(NameValueCollection).Assembly);
				var factory = new HostingEnvironmentFactory(x => x.Name.StartsWith("tmp_"));
				var hostingEnvironment = factory.Create(new[] {a1, a2});

				IRunnable a1Runnable;
				using (hostingEnvironment.CreateHost(a1).StartHosting(out a1Runnable))
					Assert.That(a1Runnable.GetType().Name, Is.EqualTo("A1Runnable"));

				IRunnable a2Runnable;
				using (hostingEnvironment.CreateHost(a2).StartHosting(out a2Runnable))
					Assert.That(a2Runnable.GetType().Name, Is.EqualTo("A2Runnable"));
			}
		}

		protected Assembly CompileAssembly(string source, params Assembly[] references)
		{
			var testAssemblyName = "tmp_" + Guid.NewGuid().ToString("N");
			var tempAssemblyFileName = testAssemblyName + ".dll";
			var compilationParameters = new CompilerParameters
			{
				OutputAssembly = tempAssemblyFileName,
				GenerateExecutable = false
			};
			foreach (var reference in references.Select(x => x.GetName().Name + ".dll"))
				compilationParameters.ReferencedAssemblies.Add(reference);
			var compilationResult = CodeDomProvider.CreateProvider("C#").CompileAssemblyFromSource(compilationParameters, source);
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
	}
}