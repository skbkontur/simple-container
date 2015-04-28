using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class AssembliesLoadTest : UnitTestBase
	{
		private AppDomain appDomain;
		private static readonly string testDirectory = Path.GetFullPath("testDirectory");

		protected override void SetUp()
		{
			base.SetUp();
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

		private void CopyAssemblyToTestDirectory(Assembly assembly)
		{
			File.Copy(assembly.Location, Path.Combine(testDirectory, Path.GetFileName(assembly.Location)));
		}
	}
}