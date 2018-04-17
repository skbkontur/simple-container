using System;
using NUnit.Framework;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class ContainerFactoryTest : UnitTestBase
	{
		public class CanSkipAssemblyFilter : ContainerFactoryTest
		{
			public class A
			{
			}

			[Test]
			public void Test()
			{
				var container = new ContainerFactory()
					.WithTypesFromDefaultBinDirectory(false)
					.WithSettingsLoader(Activator.CreateInstance)
					.Build();
				Assert.That(container.Get<A>(), Is.Not.Null);
			}
		}

		public class InlineConfigurationsAreNotShared : ContainerFactoryTest
		{
			public class A
			{
				public readonly int parameter;

				public A(int parameter = -1)
				{
					this.parameter = parameter;
				}
			}

			[Test]
			public void Test()
			{
				var f = new ContainerFactory()
					.WithTypesFromDefaultBinDirectory(false)
					.WithSettingsLoader(Activator.CreateInstance);
				using (var c1 = f.WithConfigurator(b => b.BindDependency<A>("parameter", 1)).Build())
					Assert.That(c1.Get<A>().parameter, Is.EqualTo(1));
				using (var c2 = f.WithConfigurator(b => { }).Build())
					Assert.That(c2.Get<A>().parameter, Is.EqualTo(-1));
			}
		}

		public class CanSpecifyAssembyFilterAfterTypes
		{
			private const string referencedCode = @"
					namespace A1
					{
						public interface ISomeInterface
						{
							void Do();
						}
					}
				";

			private const string code1 = @"
					namespace A1
					{
						public class TestClass1: ISomeInterface
						{
							void ISomeInterface.Do()
							{
							}
						}
					}
				";

			private const string code2 = @"
					namespace A1
					{
						public class TestClass2: ISomeInterface
						{
							void ISomeInterface.Do()
							{
							}
						}
					}
				";

			[Test]
			public void Test()
			{
				var referencedAssembly = AssemblyCompiler.CompileAssembly(referencedCode);
				var a1 = AssemblyCompiler.CompileAssembly(code1, referencedAssembly);
				var a2 = AssemblyCompiler.CompileAssembly(code2, referencedAssembly);
				var factory = new ContainerFactory()
					.WithTypesFromAssemblies(new[] {a1, a2})
					.WithAssembliesFilter(x => x.Name == a2.GetName().Name);
				using (var container = factory.Build())
				{
					var interfaceType = referencedAssembly.GetType("A1.ISomeInterface");
					Assert.That(container.Get(interfaceType).GetType().Name, Is.EqualTo("TestClass2"));
				}
			}
		}

		public class DefaultAssemblyFilterAcceptsEverything
		{
			private const string referencedCode = @"
					namespace A1
					{
						public interface ISomeInterface
						{
							void Do();
						}
					}
				";

			private const string code = @"
					namespace A1
					{
						public class SomeInterfaceImpl: ISomeInterface
						{
							void ISomeInterface.Do()
							{
							}
						}
					}
				";

			[Test]
			public void Test()
			{
				var referencedAssembly = AssemblyCompiler.CompileAssembly(referencedCode);
				var assembly = AssemblyCompiler.CompileAssembly(code, referencedAssembly);
				var factory = new ContainerFactory().WithTypesFromAssemblies(new[] {assembly});
				using (var container = factory.Build())
				{
					var interfaceType = referencedAssembly.GetType("A1.ISomeInterface");
					Assert.That(container.Get(interfaceType).GetType().Name, Is.EqualTo("SomeInterfaceImpl"));
				}
			}
		}
	}
}