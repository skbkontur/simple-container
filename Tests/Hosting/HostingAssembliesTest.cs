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
	public abstract class HostingAssembliesTest : UnitTestBase
	{
		public class ImplementationsFromIndependentPrimaryAssemblies : HostingAssembliesTest
		{
			private const string a1Code = @"
					using System.Collections.Specialized;
					using SimpleContainer.Hosting;

					namespace A1
					{
						public class A1Component: IComponent
						{
							public void Run(ComponentHostingOptions arguments)
							{
							}
						}
					}
				";

			private const string a2Code = @"
					using System.Collections.Specialized;
					using SimpleContainer.Hosting;

					namespace A2
					{
						public class A2Component: IComponent
						{
							public void Run(ComponentHostingOptions arguments)
							{
							}
						}
					}
				";

			[Test]
			public void Test()
			{
				var a1 = CompileAssembly(a1Code);
				var a2 = CompileAssembly(a2Code);
				var hostingEnvironment = Factory().FromAssemblies(new[] {a1, a2});

				IComponent a1Component;
				using (hostingEnvironment.CreateHost(a1, null).StartHosting(out a1Component))
					Assert.That(a1Component.GetType().Name, Is.EqualTo("A1Component"));

				IComponent a2Component;
				using (hostingEnvironment.CreateHost(a2, null).StartHosting(out a2Component))
					Assert.That(a2Component.GetType().Name, Is.EqualTo("A2Component"));
			}
		}

		public class AcceptImplementationFromReferencedAssembly : HostingAssembliesTest
		{
			private const string a1Code = @"
					using System.Collections.Specialized;
					using SimpleContainer.Hosting;

					namespace A1
					{
						public class Component1: IComponent
						{
							public void Run(ComponentHostingOptions options)
							{
							}
						}
					}
				";

			private const string a2Code = @"
					namespace A2
					{
						public class Impl2
						{
							public Impl2(A1.Component1 doNotAllowsCompilerRemoveReferenceToA1)
							{
							}
						}
					}
				";

			[Test]
			public void Test()
			{
				var a1 = CompileAssembly(a1Code);
				var a2 = CompileAssembly(a2Code, a1);
				var hostingEnvironment = Factory().FromAssemblies(new[] {a1, a2});

				IComponent a1Component;
				using (hostingEnvironment.CreateHost(a2, null).StartHosting(out a1Component))
					Assert.That(a1Component.GetType().Name, Is.EqualTo("Component1"));
			}
		}

		public class AcceptImplementationFromAssemblyReferencedViaAttribute : HostingAssembliesTest
		{
			private const string a1Code = @"
					using System.Collections.Specialized;
					using SimpleContainer.Hosting;

					namespace A1
					{
						public class Component1: IComponent
						{
							public void Run(ComponentHostingOptions options)
							{
							}
						}
					}
				";

			private const string a2CodeFormat = @"
					using SimpleContainer.Hosting;

					[assembly: ContainerReference(""{0}"")]

					namespace A2
					{{
						public class Impl2
						{{
						}}
					}}
				";

			[Test]
			public void Test()
			{
				var a1 = CompileAssembly(a1Code);
				var a2 = CompileAssembly(string.Format(a2CodeFormat, a1.GetName().Name), a1);
				var hostingEnvironment = Factory().FromAssemblies(new[] {a1, a2});

				IComponent a1Component;
				using (hostingEnvironment.CreateHost(a2, null).StartHosting(out a1Component))
					Assert.That(a1Component.GetType().Name, Is.EqualTo("Component1"));
			}
		}

		public class DoNotUseConfiguratorsFromUnreferencedAssemblies : HostingAssembliesTest
		{
			private const string mainAssemblyCode = @"
					using SimpleContainer.Hosting;
					using System;

					namespace A1
					{
						public class Impl1: IServiceProvider
						{
							public object GetService(Type serviceType)
							{
								return null;
							}
						}

						public class Impl2: IServiceProvider
						{
							public object GetService(Type serviceType)
							{
								return null;
							}
						}
					}
				";

			private const string unreferencedAssemblyCode = @"
					using SimpleContainer.Hosting;
					using SimpleContainer;
					using System;
					using A1;

					namespace A2
					{
						public class ServiceConfigurator: IServiceConfigurator<IServiceProvider>
						{
							public void Configure(ServiceConfigurationBuilder<IServiceProvider> builder)
							{
								builder.Bind<Impl1>();
							}
						}
					}
				";

			private const string entryAssemblyCode = @"
					using SimpleContainer.Hosting;
					using System;

					[assembly: ContainerReference(""{0}"")]

					namespace A3
					{{
						public class Component: IComponent
						{{
							public Component(IServiceProvider serviceProvider)
							{{
							}}

							public void Run(ComponentHostingOptions arguments)
							{{
							}}
						}}
					}}
				";

			[Test]
			public void Test()
			{
				var mainAssembly = CompileAssembly(mainAssemblyCode);
				var unreferencedAssembly = CompileAssembly(unreferencedAssemblyCode, mainAssembly);
				var entryAssembly = CompileAssembly(string.Format(entryAssemblyCode, mainAssembly.GetName().Name), mainAssembly);

				var hostingEnvironment = Factory().FromAssemblies(new[] {mainAssembly, unreferencedAssembly, entryAssembly});

				IComponent component;
				var containerHost = hostingEnvironment.CreateHost(entryAssembly, null);
				var error = Assert.Throws<SimpleContainerException>(() => containerHost.StartHosting(out component));
				Assert.That(error.Message, Is.StringContaining("many implementations for IServiceProvider\r\n\tImpl1\r\n\tImpl2"));
			}
		}

		public class LocalConfiguratorsExecuteLast : HostingAssembliesTest
		{
			private const string referencedAssemblycode = @"
					using SimpleContainer.Hosting;
					using SimpleContainer;
					using System;

					namespace A1
					{
						public class Impl1: IServiceProvider
						{
							public object GetService(Type serviceType)
							{
								return null;
							}
						}

						public class Impl2: IServiceProvider
						{
							public object GetService(Type serviceType)
							{
								return null;
							}
						}
				
						public class ServiceConfigurator: IServiceConfigurator<IServiceProvider>
						{
							public void Configure(ServiceConfigurationBuilder<IServiceProvider> builder)
							{
								builder.Bind<Impl1>();
							}
						}
					}
				";

			private const string primaryAssemblyCode = @"
					using SimpleContainer.Hosting;
					using SimpleContainer;
					using System;
					using A1;

					namespace A2
					{
						public class ServiceConfigurator: IServiceConfigurator<IServiceProvider>
						{
							public void Configure(ServiceConfigurationBuilder<IServiceProvider> builder)
							{
								builder.Bind<Impl2>(true);
							}
						}
					}
				";

			[Test]
			public void Test()
			{
				var a1 = CompileAssembly(referencedAssemblycode);
				var a2 = CompileAssembly(primaryAssemblyCode, a1);
				var hostingEnvironment = Factory().FromAssemblies(new[] {a1, a2});

				IServiceProvider serviceProvider;
				using (hostingEnvironment.CreateHost(a2, null).StartHosting(out serviceProvider))
					Assert.That(serviceProvider.GetType().Name, Is.EqualTo("Impl2"));
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
			var defaultAssemblies = new[]
			{
				Assembly.GetExecutingAssembly(),
				typeof (IComponent).Assembly,
				typeof (NameValueCollection).Assembly
			};
			foreach (var reference in references.Concat(defaultAssemblies).Select(x => x.GetName().Name + ".dll"))
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

		protected static HostingEnvironmentFactory Factory()
		{
			return new HostingEnvironmentFactory(x => x.Name.StartsWith("tmp_"));
		}
	}
}