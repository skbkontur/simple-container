using System;
using System.Reflection;
using NUnit.Framework;

namespace SimpleContainer.Tests.Hosting
{
	public abstract class DisposeTest : SimpleContainerTestBase
	{
		public class DisposeAllServicesEvenIfSomeOfThemCrashed : ContainerDependenciesTest
		{
			public class Component1 : IDisposable
			{
				public readonly Component2 component2;

				public Component1(Component2 component2)
				{
					this.component2 = component2;
				}

				public void Dispose()
				{
					LogBuilder.AppendLine("Component1.OnStop ");
					throw new InvalidOperationException("test component1 crash");
				}
			}

			public class Component2 : IDisposable
			{
				public void Dispose()
				{
					LogBuilder.AppendLine("Component2.OnStop ");
					throw new InvalidOperationException("test component2 crash");
				}
			}

			[Test]
			public void Test()
			{
				using (var staticContainer = CreateStaticContainer())
				{
					var container = staticContainer.CreateLocalContainer(Assembly.GetExecutingAssembly(), null);
					container.Get<Component1>();
					var error = Assert.Throws<AggregateException>(container.Dispose);
					Assert.That(error.Message, Is.EqualTo("error disposing services"));
					Assert.That(error.InnerExceptions[0].Message, Is.EqualTo("error disposing [Component1]"));
					Assert.That(error.InnerExceptions[0].InnerException.Message, Is.EqualTo("test component1 crash"));
					Assert.That(error.InnerExceptions[1].Message, Is.EqualTo("error disposing [Component2]"));
					Assert.That(error.InnerExceptions[1].InnerException.Message, Is.EqualTo("test component2 crash"));
				}
			}
		}
	}
}