using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class AssembliesLoadTest : UnitTestBase
	{
		protected override void SetUp()
		{
			base.SetUp();
			testDirectory = Path.GetFullPath($"{testDirectory}{Guid.NewGuid():N}");
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

		private AppDomain appDomain;
		private string testDirectory;

		private void CopyAssemblyToTestDirectory(string assembly)
		{
			File.Copy(assembly, Path.Combine(testDirectory, Path.GetFileName(assembly)));
		}

		private FactoryInvoker GetInvoker()
		{
			return (FactoryInvoker) appDomain.CreateInstanceAndUnwrap(Assembly.GetExecutingAssembly().GetName().FullName,
				typeof (FactoryInvoker).FullName);
		}

		private class FactoryInvoker : MarshalByRefObject
		{
			public string CreateCointainerWithCrash()
			{
				try
				{
					CreateContainer();
					return "can't reach here";
				}
				catch (Exception e)
				{
					return e.ToString();
				}
			}

			public void DoCallBack<T>(T parameter, Action<T> action)
			{
				action(parameter);
			}

			private void CreateContainer()
			{
				new ContainerFactory()
					.WithAssembliesFilter(x => x.Name.StartsWith("tmp_"))
					.WithTypesFromDefaultBinDirectory(false)
					.Build();
			}
		}

		public class DisplayScannedAssembliesInException : AssembliesLoadTest
		{
			private const string primaryAssemblyCode = @"
					using System;
					using A1;

					namespace A1
					{
						public interface ISomeInterface
						{
						}
					}
				";


			[Test]
			public void Test()
			{
				var primaryAssembly = AssemblyCompiler.Compile(primaryAssemblyCode);
				var assemblyName = Path.GetFileNameWithoutExtension(primaryAssembly);

				CopyAssemblyToTestDirectory(primaryAssembly);
				CopyAssemblyToTestDirectory(typeof (IContainer).Assembly.Location);
				CopyAssemblyToTestDirectory(Assembly.GetExecutingAssembly().Location);
				CopyAssemblyToTestDirectory(typeof (Assert).Assembly.Location);

				GetInvoker().DoCallBack(assemblyName, delegate(string s)
				{
					var f = new ContainerFactory()
						.WithAssembliesFilter(x => x.Name.StartsWith("tmp_"))
						.WithTypesFromDefaultBinDirectory(false);
					var type = Type.GetType("A1.ISomeInterface, " + s);
					Assert.That(type, Is.Not.Null);
					using (var c = f.Build())
					{
						var exception = Assert.Throws<SimpleContainerException>(() => c.Get(type));
						var assemblies = new[] {"SimpleContainer", s}
							.OrderBy(x => x)
							.Select(x => "\t" + x)
							.JoinStrings("\r\n");
						var expectedMessage = TestHelpers.FormatMessage(@"
no instances for [ISomeInterface]
!ISomeInterface - has no implementations
scanned assemblies
" + assemblies);
						Assert.That(exception.Message, Is.EqualTo(expectedMessage));
					}
				});
			}
		}

		public class CorrectExceptionHandling : AssembliesLoadTest
		{
			private string referencedAssemblyCodeV1 = @"
using SimpleContainer.Configuration;
using SimpleContainer;
using System;
namespace A1
{
	public interface ISomeInterface
	{
	}
}";

			private string referencedAssemblyCodeV2 = @"
using SimpleContainer.Configuration;
using SimpleContainer;
using System;
namespace A1
{
	public interface ISomeInterface
	{
		void Do();
	}
}";

			private string primaryAssemblyCode = @"
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
}";


			[Test]
			public void Test()
			{
				var referencedAssemblyV2 = AssemblyCompiler.Compile(referencedAssemblyCodeV2);
				var primaryAssembly = AssemblyCompiler.Compile(primaryAssemblyCode, referencedAssemblyV2);
				AssemblyCompiler.CompileTo(referencedAssemblyCodeV1, referencedAssemblyV2);

				CopyAssemblyToTestDirectory(referencedAssemblyV2);
				CopyAssemblyToTestDirectory(primaryAssembly);
				CopyAssemblyToTestDirectory(typeof (IContainer).Assembly.Location);
				CopyAssemblyToTestDirectory(Assembly.GetExecutingAssembly().Location);

				var exceptionText = GetInvoker().CreateCointainerWithCrash();

				const string englishText = "Unable to load one or more of the requested types";
				const string russianText = "Не удается загрузить один или более запрошенных типов";
				Assert.That(exceptionText, Does.Contain(englishText).Or.StringContaining(russianText));
				Assert.That(exceptionText, Does.Contain(Path.GetFileNameWithoutExtension(primaryAssembly)));
			}
		}
	}
}
