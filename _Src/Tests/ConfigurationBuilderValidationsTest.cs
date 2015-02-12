using System;
using NUnit.Framework;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class ConfigurationBuilderValidationsTest : SimpleContainerTestBase
	{
		public class BindToInvalidImplementation : ConfigurationBuilderValidationsTest
		{
			[Test]
			public void Test()
			{
				var error = Assert.Throws<SimpleContainerException>(() => Container(x => x.Bind(typeof (A), typeof (B))));
				Assert.That(error.Message, Is.StringContaining("[A] is not assignable from [B]"));
			}

			public class A
			{
			}

			public class B
			{
			}

			public class Wrap
			{
				public Wrap(A a)
				{
				}
			}
		}

		public class CannotBindValueForOpenGeneric : NotConfiguredGenericsTest
		{
			public class A<T>
			{
			}

			[Test]
			public void Test()
			{
				Exception exception = null;
				Container(b => exception = Assert.Throws<SimpleContainerException>(() => b.Bind(typeof (A<>), new A<int>())));
				Assert.That(exception, Is.Not.Null);
				Assert.That(exception.Message, Is.EqualTo("can't bind value for generic definition [A<T>]"));
			}
		}
		
		public class CannotBindFactoryForOpenGeneric : NotConfiguredGenericsTest
		{
			public class A<T>
			{
			}

			[Test]
			public void Test()
			{
				Exception exception = null;
				Container(b => exception = Assert.Throws<SimpleContainerException>(() => b.Bind(typeof (A<>), new A<int>())));
				Assert.That(exception, Is.Not.Null);
				Assert.That(exception.Message, Is.EqualTo("can't bind value for generic definition [A<T>]"));
			}
		}

		public class BindInvalidParameterValueOfSimpleType : BasicTest
		{
			[Test]
			public void Test()
			{
				var error = Assert.Throws<SimpleContainerException>(() => Container(x => x.BindDependency<Wrap, A>(42)));
				Assert.That(error.Message,
					Is.StringContaining("dependency [42] of type [Int32] for service [Wrap] can't be casted to required type [A]"));
			}

			public class A
			{
			}

			public class B
			{
			}

			public class Wrap
			{
				public Wrap(A a)
				{
				}
			}
		}

		public class BindInvalidParameterValue : BasicTest
		{
			[Test]
			public void Test()
			{
				var error = Assert.Throws<SimpleContainerException>(() => Container(x => x.BindDependency<Wrap, A>(new B())));
				Assert.That(error.Message,
					Is.StringContaining("dependency of type [B] for service [Wrap] can't be casted to required type [A]"));
			}

			public class A
			{
			}

			public class B
			{
			}

			public class Wrap
			{
				public Wrap(A a)
				{
				}
			}
		}
	}
}