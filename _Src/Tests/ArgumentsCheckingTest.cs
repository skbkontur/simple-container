using System;
using NUnit.Framework;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class ArgumentsCheckingTest : SimpleContainerTestBase
	{
		public class ExplicitArgumentNullExceptionForNullType : ArgumentsCheckingTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var exception = Assert.Throws<ArgumentNullException>(() => container.Get(null));
				Assert.That(exception.ParamName, Is.EqualTo("type"));
			}
		}

		public class ExplicitArgumentNullExceptionForGetImplementationsOf : ArgumentsCheckingTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var exception = Assert.Throws<ArgumentNullException>(() => container.GetImplementationsOf(null));
				Assert.That(exception.ParamName, Is.EqualTo("interfaceType"));
			}
		}

		public class NullContractsAreEquivalentToEmpty : ArgumentsCheckingTest
		{
			public class A
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.DoesNotThrow(() => container.Resolve<A>(null));
			}
		}

		public class ExplicitArgumentNullExceptionForBuildUp : ArgumentsCheckingTest
		{
			public class A
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var exception = Assert.Throws<ArgumentNullException>(() => container.BuildUp(null, new string[0]));
				Assert.That(exception.ParamName, Is.EqualTo("target"));

				var conainerException = Assert.Throws<SimpleContainerException>(() => container.BuildUp(this, new string[] {null}));
				Assert.That(conainerException.Message, Is.EqualTo("invalid contracts [] - empty ones found"));
			}
		}

		public class InputContractsDuplications : ArgumentsCheckingTest
		{
			public class A
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var exception = Assert.Throws<SimpleContainerException>(() => container.Resolve<A>("x", "y", "x"));
				Assert.That(exception.Message, Is.EqualTo("invalid contracts [x,y,x] - duplicates found"));
			}
		}

		public class InputContractsDuplicationWithClassContract : ArgumentsCheckingTest
		{
			[TestContract("a")]
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
				var container = Container(b => b.Contract("a").BindDependency<A>("parameter", 67));
				var instance = container.Resolve<A>("a");
				Assert.That(instance.Single().parameter, Is.EqualTo(67));
				Assert.That(instance.GetConstructionLog(), Is.EqualTo("A[a]\r\n\tparameter -> 67"));
			}
		}

		public class ExplicitArgumentNullExceptionForCreate : ArgumentsCheckingTest
		{
			public class A
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var exception = Assert.Throws<ArgumentNullException>(() => container.Create(null));
				Assert.That(exception.ParamName, Is.EqualTo("type"));

				var conainerException =
					Assert.Throws<SimpleContainerException>(() => container.Create(typeof (A), new string[] {null}, null));
				Assert.That(conainerException.Message, Is.EqualTo("invalid contracts [] - empty ones found"));
			}
		}

		public class ExplicitExceptionForNullContract : ArgumentsCheckingTest
		{
			public class A
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var containerException = Assert.Throws<SimpleContainerException>(() => container.Resolve<A>(new[] {"a", null}));
				Assert.That(containerException.Message, Is.EqualTo("invalid contracts [a,] - empty ones found"));
			}
		}
	}
}