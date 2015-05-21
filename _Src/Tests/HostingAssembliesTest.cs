using System;
using NUnit.Framework;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class HostingAssembliesTest : UnitTestBase
	{
		public class ImplementationsFromIndependentPrimaryAssemblies : HostingAssembliesTest
		{
			private const string a1Code = @"
					using System.Collections.Specialized;
					using SimpleContainer.Interface;

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
					using SimpleContainer.Interface;

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
					using (var localContainer = staticContainer.CreateLocalContainer(null, a1, null, null))
						Assert.That(localContainer.Get<IComponent>().GetType().Name, Is.EqualTo("A1Component"));

					using (var localContainer = staticContainer.CreateLocalContainer(null, a2, null, null))
						Assert.That(localContainer.Get<IComponent>().GetType().Name, Is.EqualTo("A2Component"));
				}
			}
		}

		public class AcceptImplementationFromReferencedAssembly : HostingAssembliesTest
		{
			private const string a1Code = @"
					using System.Collections.Specialized;
					using SimpleContainer.Interface;

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
				using (var localContainer = staticContainer.CreateLocalContainer(null, a2, null, null))
					Assert.That(localContainer.Get<IComponent>().GetType().Name, Is.EqualTo("Component1"));
			}
		}

		public class AcceptImplementationFromAssemblyReferencedViaAttribute : HostingAssembliesTest
		{
			private const string a1Code = @"
					using System.Collections.Specialized;
					using SimpleContainer.Interface;

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
				using (var localContainer = staticContainer.CreateLocalContainer(null, a2, null, null))
					Assert.That(localContainer.Get<IComponent>().GetType().Name, Is.EqualTo("Component1"));
			}
		}

		public class DoNotUseConfiguratorsFromUnreferencedAssemblies : HostingAssembliesTest
		{
			private const string mainAssemblyCode = @"
					using SimpleContainer.Interface;
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
					using SimpleContainer.Interface;
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
				var entryAssembly = AssemblyCompiler.Compile(string.Format(entryAssemblyCode, mainAssembly.GetName().Name),
					mainAssembly);

				using (var staticContainer = Factory().FromAssemblies(new[] {mainAssembly, unreferencedAssembly, entryAssembly}))
				using (var localContainer = staticContainer.CreateLocalContainer(null, entryAssembly, null, null))
				{
					var error = Assert.Throws<SimpleContainerException>(() => localContainer.Get<IComponent>());
					Assert.That(error.Message, Is.StringContaining("many implementations for [IServiceProvider]\r\n\tImpl1\r\n\tImpl2"));
				}
			}
		}

		public class UseAssembliesFilterForExplicitlySpecifiedAssemblies : HostingAssembliesTest
		{
			private const string primaryAssemblyCode = @"
					using SimpleContainer.Interface;

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
				using (var localContainer = staticContainer.CreateLocalContainer(null, a1, null, null))
					Assert.That(localContainer.GetAll<IComponent>(), Is.Empty);
			}
		}

		public class PluginsTest : HostingAssembliesTest
		{
			private const string primaryAssemblyCode = @"
					namespace A1
					{
						public interface ITestInterface
						{
						}

						public class DefaultImpl: ITestInterface
						{
						}
					}
				";

			private const string pluginAssemblyCode = @"
					using SimpleContainer.Configuration;

					namespace A1
					{
						public class TestingImpl: ITestInterface
						{
						}

						public class ServiceConfigurator: IServiceConfigurator<ITestInterface>
						{
							public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<ITestInterface> builder)
							{
								builder.Bind<TestingImpl>();
							}
						}
					}
				";

			[Test]
			public void Test()
			{
				var primary = AssemblyCompiler.Compile(primaryAssemblyCode);
				var plugin = AssemblyCompiler.Compile(pluginAssemblyCode, primary);
				var factory = new ContainerFactory()
					.WithAssembliesFilter(x => x.Name.StartsWith("tmp_"))
					.WithPlugin(plugin);
				using (var staticContainer = factory.FromAssemblies(new[] {primary}))
				using (var localContainer = staticContainer.CreateLocalContainer(null, primary, null, null))
					Assert.That(localContainer.Get(primary.GetType("A1.ITestInterface")).GetType().Name, Is.EqualTo("TestingImpl"));
			}
		}

		private static ContainerFactory Factory()
		{
			return new ContainerFactory().WithAssembliesFilter(x => x.Name.StartsWith("tmp_"));
		}
	}
}