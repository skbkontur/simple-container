using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class AssembliesLoadTest : UnitTestBase
	{
		protected AppDomain appDomain;
		protected string testDirectory;

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

		protected void CopyAssemblyToTestDirectory(Assembly assembly)
		{
			File.Copy(assembly.Location, Path.Combine(testDirectory, Path.GetFileName(assembly.Location)));
		}

		protected FactoryInvoker GetInvoker()
		{
			return (FactoryInvoker) appDomain.CreateInstanceAndUnwrap(Assembly.GetExecutingAssembly().GetName().FullName,
				typeof (FactoryInvoker).FullName);
		}

		public class FactoryInvoker : MarshalByRefObject
		{
			public string CreateLocalContainerWithCrash(string primaryAssemblyName)
			{
				var staticContainer = CreateStaticCointainer();
				try
				{
					var primaryAssembly = AppDomain.CurrentDomain.GetAssemblies().Single(x => x.GetName().Name == primaryAssemblyName);
					staticContainer.CreateLocalContainer(null, primaryAssembly, null, null);
					return "can't reach here";
				}
				catch (Exception e)
				{
					return e.ToString();
				}
			}

			public string CreateStaticCointainerWithCrash()
			{
				try
				{
					CreateStaticCointainer();
					return "can't reach here";
				}
				catch (Exception e)
				{
					return e.ToString();
				}
			}

			private static IStaticContainer CreateStaticCointainer()
			{
				return new ContainerFactory()
					.WithAssembliesFilter(x => x.Name.StartsWith("tmp_"))
					.FromDefaultBinDirectory(false);
			}
		}

		public class CorrectExceptionHandling : AssembliesLoadTest
		{
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

				var exceptionText = GetInvoker().CreateStaticCointainerWithCrash();
				Assert.That(exceptionText, Is.StringContaining("A1.ISomeInterface.Do"));

				const string englishText = "Unable to load one or more of the requested types";
				const string russianText = "Не удается загрузить один или более запрошенных типов";
				Assert.That(exceptionText, Is.StringContaining(englishText).Or.StringContaining(russianText));
				Assert.That(exceptionText, Is.StringContaining(primaryAssembly.GetName().Name));
			}
		}

		public class DisplayReferenceChainForAssemblyLoadExceptions : AssembliesLoadTest
		{
			private const string a1Code = @"
				";

			private const string a2Code = @"
					using SimpleContainer.Infection;

					[assembly: ContainerReference(""{0}"")]
				";

			private const string a3Code = @"
					using SimpleContainer.Infection;

					[assembly: ContainerReference(""{0}"")]
				";

			[Test]
			public void Test()
			{
				var a1 = AssemblyCompiler.Compile(a1Code);
				var a2 = AssemblyCompiler.Compile(string.Format(a2Code, a1.GetName().Name), a1);
				var a3 = AssemblyCompiler.Compile(string.Format(a3Code, a2.GetName().Name), a2);

				CopyAssemblyToTestDirectory(a2);
				CopyAssemblyToTestDirectory(a3);
				CopyAssemblyToTestDirectory(typeof(IContainer).Assembly);
				CopyAssemblyToTestDirectory(Assembly.GetExecutingAssembly());

				var exceptionText = GetInvoker().CreateLocalContainerWithCrash(a3.GetName().Name);
				var exceptionKey = string.Format("exception loading assembly [{0}], reference chain [{1}]->[{2}]",
					a1.GetName().Name, a3.GetName().Name, a2.GetName().Name);
				Assert.That(exceptionText, Is.StringContaining(exceptionKey));
			}
		}
	}
}