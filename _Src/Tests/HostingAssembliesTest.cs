using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using SimpleContainer.Hosting;
using SimpleContainer.Implementation;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
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
							public void Run()
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
							public void Run()
							{
							}
						}
					}
				";

			[Test]
			public void Test()
			{
				var a1 = AssemblyCompiler.Compile(a1Code);
				var a2 = AssemblyCompiler.Compile(a2Code);
				using (var staticContainer = Factory().FromAssemblies(new[] {a1, a2}))
				{
					using (var localContainer = staticContainer.CreateLocalContainer(null, a1, null))
						Assert.That(localContainer.Get<IComponent>().GetType().Name, Is.EqualTo("A1Component"));

					using (var localContainer = staticContainer.CreateLocalContainer(null, a2, null))
						Assert.That(localContainer.Get<IComponent>().GetType().Name, Is.EqualTo("A2Component"));
				}
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
							public void Run()
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
				var a1 = AssemblyCompiler.Compile(a1Code);
				var a2 = AssemblyCompiler.Compile(a2Code, a1);
				using (var staticContainer = Factory().FromAssemblies(new[] {a1, a2}))
				using (var localContainer = staticContainer.CreateLocalContainer(null, a2, null))
					Assert.That(localContainer.Get<IComponent>().GetType().Name, Is.EqualTo("Component1"));
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
							public void Run()
							{
							}
						}
					}
				";

			private const string a2CodeFormat = @"
					using SimpleContainer.Infection;

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
				var a1 = AssemblyCompiler.Compile(a1Code);
				var a2 = AssemblyCompiler.Compile(string.Format(a2CodeFormat, a1.GetName().Name), a1);
				using (var staticContainer = Factory().FromAssemblies(new[] {a1, a2}))
				using (var localContainer = staticContainer.CreateLocalContainer(null, a2, null))
					Assert.That(localContainer.Get<IComponent>().GetType().Name, Is.EqualTo("Component1"));
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
					using SimpleContainer.Configuration;
					using SimpleContainer;
					using System;
					using A1;

					namespace A2
					{
						public class ServiceConfigurator: IServiceConfigurator<IServiceProvider>
						{
							public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<IServiceProvider> builder)
							{
								builder.Bind<Impl1>();
							}
						}
					}
				";

			private const string entryAssemblyCode = @"
					using SimpleContainer.Hosting;
					using SimpleContainer.Infection;
					using System;

					[assembly: ContainerReference(""{0}"")]

					namespace A3
					{{
						public class Component: IComponent
						{{
							public Component(IServiceProvider serviceProvider)
							{{
							}}

							public void Run()
							{{
							}}
						}}
					}}
				";

			[Test]
			public void Test()
			{
				var mainAssembly = AssemblyCompiler.Compile(mainAssemblyCode);
				var unreferencedAssembly = AssemblyCompiler.Compile(unreferencedAssemblyCode, mainAssembly);
				var entryAssembly = AssemblyCompiler.Compile(string.Format(entryAssemblyCode, mainAssembly.GetName().Name), mainAssembly);

				using (var staticContainer = Factory().FromAssemblies(new[] {mainAssembly, unreferencedAssembly, entryAssembly}))
				using (var localContainer = staticContainer.CreateLocalContainer(null, entryAssembly, null))
				{
					var error = Assert.Throws<SimpleContainerException>(() => localContainer.Get<IComponent>());
					Assert.That(error.Message, Is.StringContaining("many implementations for IServiceProvider\r\n\tImpl1\r\n\tImpl2"));
				}
			}
		}

		public class LocalConfiguratorsExecuteLast : HostingAssembliesTest
		{
			private const string referencedAssemblycode = @"
					using SimpleContainer.Configuration;
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
							public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<IServiceProvider> builder)
							{
								builder.Bind<Impl1>();
							}
						}
					}
				";

			private const string primaryAssemblyCode = @"
					using SimpleContainer.Configuration;
					using System;
					using A1;

					namespace A2
					{
						public class ServiceConfigurator: IServiceConfigurator<IServiceProvider>
						{
							public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<IServiceProvider> builder)
							{
								builder.Bind<Impl2>(true);
							}
						}
					}
				";

			[Test]
			public void Test()
			{
				var a1 = AssemblyCompiler.Compile(referencedAssemblycode);
				var a2 = AssemblyCompiler.Compile(primaryAssemblyCode, a1);
				using (var staticContainer = Factory().FromAssemblies(new[] {a2, a1}))
				using (var localContainer = staticContainer.CreateLocalContainer(null, a2, null))
					Assert.That(localContainer.Get<IServiceProvider>().GetType().Name, Is.EqualTo("Impl2"));
			}
		}

		public class TypeLoadExceptionCorrectHandling : HostingAssembliesTest
		{
			private AppDomain appDomain;
			private const string referencedAssemblyCodeV1 = @"
					using SimpleContainer.Configuration;
					using SimpleContainer;
					using System;

					namespace A1
					{
						public interface ISomeInterface
						{
						}
					}
				";

			private const string referencedAssemblyCodeV2 = @"
					using SimpleContainer.Configuration;
					using SimpleContainer;
					using System;

					namespace A1
					{
						public interface ISomeInterface
						{
							void Do();
						}
					}
				";

			private const string primaryAssemblyCode = @"
					using System;
					using A1;

					namespace A2
					{
						public class TestClass: ISomeInterface
						{
							void ISomeInterface.Do()
							{
							}
						}
					}
				";

			private string testDirectory;

			protected override void SetUp()
			{
				base.SetUp();
				testDirectory = Path.GetFullPath("testDirectory");
				if (Directory.Exists(testDirectory))
					Directory.Delete(testDirectory, true);
				Directory.CreateDirectory(testDirectory);
				appDomain = AppDomain.CreateDomain("test", null, new AppDomainSetup {ApplicationBase = testDirectory});
			}

			protected override void TearDown()
			{
				if (appDomain != null)
					AppDomain.Unload(appDomain);
				if (Directory.Exists(testDirectory))
					Directory.Delete(testDirectory, true);
				base.TearDown();
			}

			private void CopyAssemblyToTestDirectory(Assembly assembly, string resultFileName = null)
			{
				File.Copy(assembly.Location, Path.Combine(testDirectory, Path.GetFileName(resultFileName ?? assembly.Location)));
			}

			[Test]
			public void Test()
			{
				var referencedAssemblyV2 = AssemblyCompiler.Compile(referencedAssemblyCodeV2);
				AssemblyCompiler.Compile(referencedAssemblyCodeV1,
					Path.Combine(testDirectory, Path.GetFileName(referencedAssemblyV2.Location)));
				var primaryAssembly = AssemblyCompiler.Compile(primaryAssemblyCode, referencedAssemblyV2);

				CopyAssemblyToTestDirectory(primaryAssembly);
				CopyAssemblyToTestDirectory(typeof (IContainer).Assembly);
				CopyAssemblyToTestDirectory(Assembly.GetExecutingAssembly());

				var invoker = (FactoryInvoker) appDomain.CreateInstanceAndUnwrap(Assembly.GetExecutingAssembly().GetName().FullName,
					typeof (FactoryInvoker).FullName);
				var exceptionText = invoker.InvokeWithCrash();
				Assert.That(exceptionText, Is.StringContaining("A1.ISomeInterface.Do"));
				Assert.That(exceptionText, Is.StringContaining("Unable to load one or more of the requested types"));
				Assert.That(exceptionText, Is.StringContaining(primaryAssembly.GetName().Name));
			}

			public class FactoryInvoker : MarshalByRefObject
			{
				public string InvokeWithCrash()
				{
					try
					{
						new ContainerFactory()
							.WithAssembliesFilter(x => x.Name.StartsWith("tmp_"))
							.FromDefaultBinDirectory(false);
						return "can't reach here";
					}
					catch (Exception e)
					{
						return e.ToString();
					}
				}
			}
		}

		public class UseAssembliesFilterForExplicitlySpecifiedAssemblies : HostingAssembliesTest
		{
			private const string primaryAssemblyCode = @"
					using SimpleContainer.Hosting;

					namespace A1
					{
						public class TestClass: IComponent
						{
							public void Run()
							{
							}
						}
					}
				";

			[Test]
			public void Test()
			{
				var a1 = AssemblyCompiler.Compile(primaryAssemblyCode);
				var factory = new ContainerFactory()
					.WithAssembliesFilter(x => x.Name.StartsWith("tmp2_"));
				using (var staticContainer = factory.FromAssemblies(new[] {a1}))
				using (var localContainer = staticContainer.CreateLocalContainer(null, a1, null))
					Assert.That(localContainer.GetAll<IComponent>(), Is.Empty);
			}
		}

		protected static ContainerFactory Factory()
		{
			return new ContainerFactory().WithAssembliesFilter(x => x.Name.StartsWith("tmp_"));
		}
	}
}