using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using SimpleContainer.Infection;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.ConstructionLog
{
	public abstract class ConstructionLogValueFormattingTest : SimpleContainerTestBase
	{
		public class DontTryToDumpResource : ConstructionLogValueFormattingTest
		{
			public class ServiceWithResource
			{
				public string streamContent;

				public ServiceWithResource([FromResource("test-resource-for-construction-log-test.txt")] Stream stream)
				{
					using (var reader = new StreamReader(stream))
						streamContent = reader.ReadLine();
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				string log = null;
				var service = container.Resolve<ServiceWithResource>();
				Assert.That(service.IsOk());
				Assert.That(() => { log = service.GetConstructionLog(); }, Throws.Nothing);
				Assert.That(log, Is.StringContaining("resource [test-resource-for-construction-log-test.txt]"));
			}
		}

		public class IgnoreNonReadableProperties : ConstructionLogValueFormattingTest
		{
			public class A
			{
				public readonly Dto dto;

				public A(Dto dto)
				{
					this.dto = dto;
				}
			}

			public class Dto
			{
				public string Prop
				{
					set { }
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.BindDependency<A>("dto", new Dto()));
				Assert.That(container.Resolve<A>().GetConstructionLog(),
					Is.EqualTo("A\r\n\tDto const"));
			}
		}

		public class CustomValueFormatterForEntireValue : ConstructionLogValueFormattingTest
		{
			public class A
			{
				public readonly Dto dto;

				public A(Dto dto)
				{
					this.dto = dto;
				}
			}

			public class Dto
			{
			}

			[Test]
			public void Test()
			{
				var f = Factory()
					.WithConfigurator(b => b.BindDependency<A>("dto", new Dto()))
					.WithValueFormatter<Dto>(x => "dumpted Dto");
				using (var container = f.Build())
					Assert.That(container.Resolve<A>().GetConstructionLog(),
						Is.EqualTo("A\r\n\tDto const -> dumpted Dto"));
			}
		}

		public class DumpTimeSpansAsSimpleTypes : ConstructionLogValueFormattingTest
		{
			public class A
			{
				public readonly TimeSpan parameter;

				public A(TimeSpan parameter)
				{
					this.parameter = parameter;
				}
			}

			[Test]
			public void Test()
			{
				var ts = TimeSpan.FromSeconds(54).Add(TimeSpan.FromMilliseconds(17));
				var container = Container(b => b.BindDependency<A>("parameter", ts));
				const string expectedConstructionLog = "A\r\n\tparameter -> 00:00:54.0170000";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expectedConstructionLog));
			}
		}

		public class DumpValuesOnlyForSimpleTypes : ConstructionLogValueFormattingTest
		{
			public class A
			{
				public readonly CancellationToken token;

				public A(CancellationToken token)
				{
					this.token = token;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.BindDependency<A>("token", CancellationToken.None));
				const string expectedConstructionLog = "A\r\n\tCancellationToken const";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expectedConstructionLog));
			}
		}

		public class DumpBooleansInLowercase : ConstructionLogValueFormattingTest
		{
			public class A
			{
				public readonly bool someBool;

				public A(bool someBool)
				{
					this.someBool = someBool;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.BindDependency<A>("someBool", true));
				const string expectedConstructionLog = "A\r\n\tsomeBool -> true";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expectedConstructionLog));
			}
		}

		public class DumpUsedSimpleTypesInConstructionLog : ConstructionLogValueFormattingTest
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
				var container = Container(b => b.BindDependency<A>("parameter", 78));
				const string expectedConstructionLog = "A\r\n\tparameter -> 78";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expectedConstructionLog));
			}
		}

		public class DumpNullablesAsSimpleTypes : ConstructionLogValueFormattingTest
		{
			public class A
			{
				public readonly int? value;

				public A(int? value)
				{
					this.value = value;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.BindDependency<A>("value", 21));
				const string expectedConstructionLog = "A\r\n\tvalue -> 21";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expectedConstructionLog));
			}
		}

		public class DumpEnumsAsSimpleTypes : ConstructionLogValueFormattingTest
		{
			public enum MyEnum
			{
				Val1,
				Val2
			}

			public class A
			{
				public readonly MyEnum value;

				public A(MyEnum value)
				{
					this.value = value;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.BindDependency<A>("value", MyEnum.Val2));
				const string expectedConstructionLog = "A\r\n\tvalue -> Val2";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expectedConstructionLog));
			}
		}

		public class CanDumpNestedSimpleTypes : ConstructionLogValueFormattingTest
		{
			public class A
			{
				public readonly Dto dto;

				public A(Dto dto)
				{
					this.dto = dto;
				}
			}

			public class Dto
			{
				public string StrVal { get; set; }
				public B ComplexType { get; set; }
				public int IntVal { get; set; }
			}

			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.BindDependency<A>("dto", new Dto
				{
					IntVal = 5,
					StrVal = "test-string",
					ComplexType = new B()
				}));
				Assert.That(container.Resolve<A>().GetConstructionLog(),
					Is.EqualTo("A\r\n\tDto const\r\n\t\tStrVal -> test-string\r\n\t\tComplexType\r\n\t\tIntVal -> 5"));
			}
		}

		public class CanRegisterCustomValueFormatters : ConstructionLogValueFormattingTest
		{
			public class A
			{
				public readonly B b;

				public A(B b)
				{
					this.b = b;
				}
			}

			public class B
			{
				public Dto Val { get; set; }
			}

			public class Dto
			{
			}

			[Test]
			public void Test()
			{
				var f = Factory()
					.WithConfigurator(b => b.BindDependency<A>("b", new B {Val = new Dto()}))
					.WithValueFormatter<Dto>(x => "dumpted Dto");
				using (var container = f.Build())
					Assert.That(container.Resolve<A>().GetConstructionLog(),
						Is.EqualTo("A\r\n\tB const\r\n\t\tVal -> dumpted Dto"));
			}
		}

		public class DumpSettingsWithFields : ConstructionLogValueFormattingTest
		{
			public class A
			{
				public readonly ASettings settings;

				public A(ASettings settings)
				{
					this.settings = settings;
				}
			}

			public class ASettings
			{
				public int port;
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.BindDependencyValue(typeof (A), typeof (ASettings), new ASettings {port = 671}));
				var actualConstructionLog = container.Resolve<A>().GetConstructionLog();
				const string expectedConstructionLog = @"
A
	ASettings const
		port -> 671";
				Assert.That(actualConstructionLog, Is.EqualTo(FormatExpectedMessage(expectedConstructionLog)));
			}
		}

		public class SettingsWithNesting : ConstructionLogValueFormattingTest
		{
			public class A
			{
				public readonly ASettings settings;

				public A(ASettings settings)
				{
					this.settings = settings;
				}
			}

			public class ASettings
			{
				public NestedSettings Nested { get; set; }
				public string StrValue { get; set; }
			}

			public class NestedSettings
			{
				public int Value { get; set; }
			}

			[Test]
			public void Test()
			{
				var s = new ASettings
				{
					StrValue = "test-str-value",
					Nested = new NestedSettings
					{
						Value = 42
					}
				};
				var container = Container(b => b.BindDependencyValue(typeof (A), typeof (ASettings), s));
				var a = container.Resolve<A>();
				const string expectedConstructionLog = @"
A
	ASettings const
		Nested
			Value -> 42
		StrValue -> test-str-value";
				Assert.That(a.GetConstructionLog(), Is.EqualTo(FormatExpectedMessage(expectedConstructionLog)));
			}
		}

		public class CanDumpArrays : ConstructionLogValueFormattingTest
		{
			public class A
			{
				public readonly string[] urls;

				public A(string[] urls)
				{
					this.urls = urls;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.BindDependency<A>("urls", new[] {"a", "b", "c"}));
				Assert.That(container.Resolve<A>().GetConstructionLog(),
					Is.EqualTo("A\r\n\turls const\r\n\t\ta\r\n\t\tb\r\n\t\tc"));
			}
		}
		
		public class CanDumpComplextArrays : ConstructionLogValueFormattingTest
		{
			public class A
			{
				public readonly SomeItem[] items;

				public A(SomeItem[] items)
				{
					this.items = items;
				}
			}

			public class SomeItem
			{
				public int x;
				public string y;
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.BindDependency<A>("items", new[]
				{
					new SomeItem {x = 1, y = "t1"},
					new SomeItem {x = 2, y = "t2"}
				}));
				const string expectedLog = @"
A
	items const
		item
			x -> 1
			y -> t1
		item
			x -> 2
			y -> t2";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(FormatExpectedMessage(expectedLog)));
			}
		}
	}
}