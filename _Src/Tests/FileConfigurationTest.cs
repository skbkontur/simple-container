using System;
using System.IO;
using NUnit.Framework;
using SimpleContainer.Infection;

namespace SimpleContainer.Tests
{
	[TestFixture]
	internal abstract class FileConfigurationTest : SimpleContainerTestBase
	{
		public class Simple : FileConfigurationTest
		{
			public interface IA
			{
			}

			public class A1 : IA
			{
			}

			public class A2 : IA
			{
			}

			[Test]
			public void Test()
			{
				var container = Container("IA -> A2");
				Assert.That(container.Get<IA>(), Is.SameAs(container.Get<A2>()));
			}
		}

		public class StringDependency : FileConfigurationTest
		{
			public class A
			{
				public readonly string parameter;

				public A(string parameter)
				{
					this.parameter = parameter;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container("A.parameter -> qq");
				Assert.That(container.Get<A>().parameter, Is.EqualTo("qq"));
			}
		}

		public class Contract : FileConfigurationTest
		{
			public class A
			{
				public readonly B bx;
				public readonly B by;
				public readonly B b;

				public A([RequireContract("x")] B bx, [RequireContract("y")] B by, B b)
				{
					this.bx = bx;
					this.by = by;
					this.b = b;
				}
			}

			public class B
			{
				public readonly int parameter;

				public B(int parameter)
				{
					this.parameter = parameter;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(@"
					[x]
					B.parameter -> 12
					[y]
					B.parameter -> 21
					[]
					B.parameter -> 111");
				var a = container.Get<A>();
				Assert.That(a.bx.parameter, Is.EqualTo(12));
				Assert.That(a.by.parameter, Is.EqualTo(21));
				Assert.That(a.b.parameter, Is.EqualTo(111));
			}
		}

		public class IntDependency : FileConfigurationTest
		{
			public class A
			{
				public readonly int parameter;

				public A(int parameter)
				{
					this.parameter = parameter;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container("A.parameter -> 42");
				Assert.That(container.Get<A>().parameter, Is.EqualTo(42));
			}
		}

		public class NullableLongDependency : FileConfigurationTest
		{
			public class A
			{
				public readonly long? parameter;

				public A(long? parameter)
				{
					this.parameter = parameter;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container("A.parameter -> 42");
				Assert.That(container.Get<A>().parameter, Is.EqualTo(42));
			}
		}

		public class DontUse : FileConfigurationTest
		{
			public interface IInterface
			{
			}

			public class A : IInterface
			{
			}

			public class B : IInterface
			{
			}

			[Test]
			public void Test()
			{
				var container = Container("B ->");
				Assert.That(container.Get<IInterface>(), Is.SameAs(container.Get<A>()));
			}
		}

		private string configFileName;

		protected override void SetUp()
		{
			configFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test.config");
			if (File.Exists(configFileName))
				File.Delete(configFileName);
			base.SetUp();
		}

		protected override void TearDown()
		{
			if (File.Exists(configFileName))
				File.Delete(configFileName);
			base.TearDown();
		}

		protected IContainer Container(string configText)
		{
			File.WriteAllText(configFileName, configText);
			var staticContainer = CreateStaticContainer(f => f.WithConfigFile(configFileName));
			var result = LocalContainer(staticContainer, null);
			disposables.Add(result);
			return result;
		}
	}
}