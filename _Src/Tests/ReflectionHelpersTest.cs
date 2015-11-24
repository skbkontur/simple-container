using NUnit.Framework;
using SimpleContainer.Helpers;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class ReflectionHelpersTest : UnitTestBase
	{
		public class GenericDeclaringClass_FormatNameOfNestedClass : ReflectionHelpersTest
		{
			public class Simple
			{
			}

			public class SimpleGeneric<T>
			{
			}

			public class ShouldNotFormat
			{
				public class DeclaringWithNotGenericNested<T>
				{
					public class NotGenericNested
					{
					}
				}
			}

			public class DeclaringWithGenericNested<TSame>
			{
				public class GenericNested<TSame>
				{
				}
			}
			
			[Test]
			public void Test()
			{
				Assert.That(typeof (Simple).FormatName(), Is.EqualTo("Simple"));
				Assert.That(typeof (SimpleGeneric<>).FormatName(), Is.EqualTo("SimpleGeneric<T>"));
				Assert.That(typeof (SimpleGeneric<int>).FormatName(), Is.EqualTo("SimpleGeneric<int>"));
				Assert.That(typeof (ShouldNotFormat.DeclaringWithNotGenericNested<int>.NotGenericNested).FormatName(),
					Is.EqualTo("DeclaringWithNotGenericNested<int>.NotGenericNested"));
				Assert.That(typeof (ShouldNotFormat.DeclaringWithNotGenericNested<>.NotGenericNested).FormatName(),
					Is.EqualTo("DeclaringWithNotGenericNested<T>.NotGenericNested"));
				Assert.That(typeof (DeclaringWithGenericNested<string>.GenericNested<int>).FormatName(),
					Is.EqualTo("DeclaringWithGenericNested<string>.GenericNested<int>"));
				Assert.That(typeof (DeclaringWithGenericNested<>.GenericNested<>).FormatName(),
					Is.EqualTo("DeclaringWithGenericNested<TSame>.GenericNested<TSame>"));
			}
		}
	}
}