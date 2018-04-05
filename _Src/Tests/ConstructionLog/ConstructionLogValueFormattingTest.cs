using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using SimpleContainer.Configuration;
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
				Assert.That(log, Does.Contain("resource [test-resource-for-construction-log-test.txt]"));
			}
		}

		public class DumpValuesForConstantServices : ConstructionLogValueFormattingTest
		{
			public class A
			{
				public readonly SomeConstantSwitch constantSwitch;

				public A(SomeConstantSwitch constantSwitch)
				{
					this.constantSwitch = constantSwitch;
				}
			}

			public class SomeConstantSwitch
			{
				public int Value { get; set; }
			}

			public class SomeConstantSwitchConfigurator : IServiceConfigurator<SomeConstantSwitch>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<SomeConstantSwitch> builder)
				{
					builder.Bind(new SomeConstantSwitch {Value = 42});
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var resolvedA = container.Resolve<A>();
				Assert.That(resolvedA.Single().constantSwitch.Value, Is.EqualTo(42));
				var expected = "A"
					+ Environment.NewLine + "\tSomeConstantSwitch const"
					+ Environment.NewLine + "\t\tValue -> 42";
				Assert.That(resolvedA.GetConstructionLog(), Is.EqualTo(expected));
			}
		}

		public class DumpParameterNamesForTypesWithCustomValueFormatting : ConstructionLogValueFormattingTest
		{
			public class MyCoolValueType
			{
				public MyCoolValueType(int value)
				{
					Value = value;
				}

				public int Value { get; private set; }
			}

			public class A
			{
				public readonly MyCoolValueType someValue;

				public A(MyCoolValueType someValue)
				{
					this.someValue = someValue;
				}
			}

			[Test]
			public void Test()
			{
				var f = Factory()
					.WithValueFormatter<MyCoolValueType>(x => "!" + x.Value + "!")
					.WithConfigurator(b => b.BindDependencies<A>(new {someValue = new MyCoolValueType(42)}));
				using (var c = f.Build())
				{
					var s = c.Resolve<A>();
					var expected = "A" + Environment.NewLine + "\tsomeValue const -> !42!";
					Assert.That(s.GetConstructionLog(), Is.EqualTo(expected));
				}
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
				var expected = "A" + Environment.NewLine + "\tDto const";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expected));
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
				{
					var expected = "A" + Environment.NewLine + "\tdto const -> dumpted Dto";
					Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expected));
				}
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
				var expectedConstructionLog = "A"
					+ Environment.NewLine + "\tparameter -> 00:00:54.0170000";
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
				var expectedConstructionLog = "A" + Environment.NewLine + "\tCancellationToken const";
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
				var expectedConstructionLog = "A" + Environment.NewLine + "\tsomeBool -> true";
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
				var expectedConstructionLog = "A" + Environment.NewLine + "\tparameter -> 78";
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
				var expectedConstructionLog = "A" + Environment.NewLine + "\tvalue -> 21";
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
				var expectedConstructionLog = "A" + Environment.NewLine + "\tvalue -> Val2";
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
				var expected = "A"
					+ Environment.NewLine + "\tDto const"
					+ Environment.NewLine + "\t\tStrVal -> test-string"
					+ Environment.NewLine + "\t\tComplexType"
					+ Environment.NewLine + "\t\tIntVal -> 5";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expected));
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
				{
					var expected = "A"
						+ Environment.NewLine + "\tB const"
						+ Environment.NewLine + "\t\tVal -> dumpted Dto";
					Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expected));
				}
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
				var expectedConstructionLog = "A"
					+ Environment.NewLine + "\tASettings const" 
					+ Environment.NewLine + "\t\tport -> 671";
				Assert.That(actualConstructionLog, Is.EqualTo(expectedConstructionLog));
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
				var expectedConstructionLog = "A"
					+ Environment.NewLine + "\tASettings const"
					+ Environment.NewLine + "\t\tNested"
					+ Environment.NewLine + "\t\t\tValue -> 42"
					+ Environment.NewLine + "\t\tStrValue -> test-str-value";
				Assert.That(a.GetConstructionLog(), Is.EqualTo(expectedConstructionLog));
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
				var expected = "A" 
					+ Environment.NewLine + "\turls const" 
					+ Environment.NewLine + "\t\ta" 
					+ Environment.NewLine + "\t\tb" 
					+ Environment.NewLine + "\t\tc";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expected));
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
				var expectedLog = "A" 
					+ Environment.NewLine + "\titems const"
					+ Environment.NewLine + "\t\titem"
					+ Environment.NewLine + "\t\t\tx -> 1"
					+ Environment.NewLine + "\t\t\ty -> t1"
					+ Environment.NewLine + "\t\titem"
					+ Environment.NewLine + "\t\t\tx -> 2"
					+ Environment.NewLine + "\t\t\ty -> t2";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expectedLog));
			}
		}
	}
}