using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Infection;

namespace SimpleContainer.Tests
{
	public abstract class BuildUpTest : SimpleContainerTestBase
	{
		public class CanInjectEnumerableByAttribute : BuildUpTest
		{
			[Inject]
			public IEnumerable<IInterface> Interfaces { get; private set; }

			[Test]
			public void Test()
			{
				var container = Container();
				container.BuildUp(this);
				Assert.That(Interfaces, Is.EquivalentTo(new IInterface[] {container.Get<Impl1>(), container.Get<Impl2>()}));
			}

			public interface IInterface
			{
			}

			public class Impl1 : IInterface
			{
			}

			public class Impl2 : IInterface
			{
			}
		}

		public class CanInjectFactory : BuildUpTest
		{
			[Inject] private Func<B> createB;

			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				container.BuildUp(this);
				Assert.That(createB(), Is.Not.Null);
			}
		}
		
		public class CanInjectFactoryWithContract : BuildUpTest
		{
			[Inject] [RequireContract("x")] private Func<B> createB;

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
				var container = Container(b => b.Contract("x").BindDependency<B>("parameter", 42));
				container.BuildUp(this);
				Assert.That(createB().parameter, Is.EqualTo(42));
			}
		}
	}
}