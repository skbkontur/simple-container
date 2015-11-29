using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Infection;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class ConstructionLogTest : SimpleContainerTestBase
	{
		public class DumpUsedSimpleTypesInConstructionLog : ConstructionLogTest
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

		public class ConstructionLogForManyImplicitDependencies : ConstructionLogTest
		{
			public class A
			{
				public IEnumerable<IB> b;

				public A(IContainer container)
				{
					b = container.GetAll<IB>();
				}
			}

			public interface IB
			{
			}

			public class B1 : IB
			{
			}

			public class B2 : IB
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var a = container.Resolve<A>();
				var constructionLog = a.GetConstructionLog();
				Console.Out.WriteLine(constructionLog);
				Assert.That(constructionLog, Is.EqualTo("A\r\n\tIContainer\r\n\t() => IB++\r\n\t\tB1\r\n\t\tB2"));
			}
		}

		public class MergeConstructionLogFromInjectedContainer : ConstructionLogTest
		{
			public class A
			{
				public readonly B b;

				public A(IContainer container)
				{
					b = container.Get<B>();
				}
			}

			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var a = container.Resolve<A>();
				Assert.That(a.Single().b, Is.SameAs(container.Get<B>()));
				Console.Out.WriteLine(a.GetConstructionLog());
				Assert.That(a.GetConstructionLog(), Is.EqualTo("A\r\n\tIContainer\r\n\t() => B"));
			}
		}

		public class DumpSimpleTypesFromFactoryInConstructionLog : ConstructionLogTest
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
				var container = Container(b => b.BindDependencyFactory<A>("parameter", _ => "qq"));
				const string expectedConstructionLog = "A\r\n\tparameter -> qq";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expectedConstructionLog));
			}
		}

		public class DumpTimeSpansAsSimpleTypes : ConstructionLogTest
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
		
		public class DumpValuesOnlyForSimpleTypes : ConstructionLogTest
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
				const string expectedConstructionLog = "A\r\n\tCancellationToken const\r\n\t\tIsCancellationRequested -> false\r\n\t\tCanBeCanceled -> false";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expectedConstructionLog));
			}
		}
		
		public class DumpBooleansInLowercase : ConstructionLogTest
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

		public class PrintFuncArgumentNameWhenInvokedFromCtor : ConstructionLogTest
		{
			public class A
			{
				public readonly int v;
				public readonly B b;

				public A(int v, Func<B> myFactory)
				{
					this.v = v;
					b = myFactory();
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
				var container = Container(c => c.BindDependency<A>("v", 76).BindDependency<B>("parameter", 67));
				var resolved = container.Resolve<A>();
				Assert.That(resolved.GetConstructionLog(), Is.EqualTo("A\r\n\tv -> 76\r\n\tFunc<B>\r\n\t() => B\r\n\t\tparameter -> 67"));
			}
		}

		public class DumpNullablesAsSimpleTypes : ConstructionLogTest
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

		public class DumpEnumsAsSimpleTypes : ConstructionLogTest
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

		public class CommentExplicitDontUse : ConstructionLogTest
		{
			public class A
			{
				public readonly B b;

				public A(B b = null)
				{
					this.b = b;
				}
			}

			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.DontUse<B>());
				var resolvedService = container.Resolve<A>();
				Assert.That(resolvedService.Single().b, Is.Null);
				Assert.That(resolvedService.GetConstructionLog(), Is.EqualTo("A\r\n\tB - DontUse -> <null>"));
			}
		}

		public class CommentIgnoreImplementation : ConstructionLogTest
		{
			public class A
			{
				public readonly B b;

				public A(B b = null)
				{
					this.b = b;
				}
			}

			[DontUseAttribute]
			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var resolvedService = container.Resolve<A>();
				Assert.That(resolvedService.Single().b, Is.Null);
				Assert.That(resolvedService.GetConstructionLog(), Is.EqualTo("A\r\n\tB - DontUse -> <null>"));
			}
		}

		public class ExplicitNull : ConstructionLogTest
		{
			public class A
			{
				public readonly string parameter;

				public A(string parameter = null)
				{
					this.parameter = parameter;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				const string expectedConstructionLog = "A\r\n\tparameter -> <null>";
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo(expectedConstructionLog));
			}
		}

		public class MergeFailedConstructionLog : ConstructionLogTest
		{
			public class A
			{
				public readonly IB b;

				public A(IB b)
				{
					this.b = b;
				}
			}

			public interface IB
			{
			}

			public class B1 : IB
			{
			}

			public class B2 : IB
			{
			}

			public class C
			{
				public readonly A a;

				public C(A a)
				{
					this.a = a;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.Throws<SimpleContainerException>(() => container.Get<A>());
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<C>());
				const string expectedMessage =
					"many instances for [IB]\r\n\tB1\r\n\tB2\r\n\r\n!C\r\n\t!A\r\n\t\tIB++\r\n\t\t\tB1\r\n\t\t\tB2";
				Assert.That(error.Message, Is.EqualTo(expectedMessage));
			}
		}

		public class CanDumpNestedSimpleTypes : ConstructionLogTest
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
					Is.EqualTo("A\r\n\tDto const\r\n\t\tStrVal -> test-string\r\n\t\tIntVal -> 5"));
			}
		}

		public class CanRegisterCustomValueFormatters : ConstructionLogTest
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
					.WithConfigurator(b=>b.BindDependency<A>("b", new B{Val = new Dto()}))
					.WithValueFormatter<Dto>(x => "dumpted Dto");
				using (var container = f.Build())
					Assert.That(container.Resolve<A>().GetConstructionLog(),
						Is.EqualTo("A\r\n\tB const\r\n\t\tVal -> dumpted Dto"));
			}
		}

		public class CanDumpArrays : ConstructionLogTest
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
				var container = Container(b => b.BindDependency<A>("urls", new [] {"a", "b", "c"}));
				Assert.That(container.Resolve<A>().GetConstructionLog(),
					Is.EqualTo("A\r\n\turls const\r\n\t\ta\r\n\t\tb\r\n\t\tc"));
			}
		}

		public class CustomValueFormatterForEntireValue : ConstructionLogTest
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

		public class DumpCommentOnlyOnce : ConstructionLogTest
		{
			public class X
			{
				public readonly IA a1;
				public readonly IA a2;

				public X(IA a1, IA a2)
				{
					this.a1 = a1;
					this.a2 = a2;
				}
			}

			public interface IA
			{
			}

			public class A1 : IA
			{
			}

			public class A2 : IA
			{
			}

			public class AConfigurator : IServiceConfigurator<IA>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<IA> builder)
				{
					builder.WithInstanceFilter(a => a.GetType() == typeof (A2));
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Resolve<X>().GetConstructionLog(),
					Is.EqualTo("X\r\n\tIA - instance filter\r\n\t\tA1\r\n\t\tA2\r\n\tIA"));
			}
		}

		public class ConstructionLogForReusedService : ConstructionLogTest
		{
			public class A
			{
			}

			public class B
			{
				public readonly A a1;
				public readonly A a2;
				public readonly A a3;

				public B(A a1, A a2, A a3)
				{
					this.a1 = a1;
					this.a2 = a2;
					this.a3 = a3;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Resolve<B>().GetConstructionLog(), Is.EqualTo("B\r\n\tA\r\n\tA\r\n\tA"));
			}
		}

		public class DisplayInitializingStatus : ConstructionLogTest
		{
			private static readonly StringBuilder log = new StringBuilder();

			public class A : IInitializable
			{
				public readonly B b;
				public static ManualResetEventSlim goInitialize = new ManualResetEventSlim();

				public A(B b)
				{
					this.b = b;
				}

				public void Initialize()
				{
					goInitialize.Wait();
					log.Append("A.Initialize ");
				}
			}

			public class B: IInitializable
			{
				public static ManualResetEventSlim goInitialize = new ManualResetEventSlim();

				public void Initialize()
				{
					goInitialize.Wait();
					log.Append("B.Initialize ");
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var t = Task.Run(() => container.Get<A>());
				Thread.Sleep(15);
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo("A, initializing ...\r\n\tB, initializing ..."));
				Assert.That(log.ToString(), Is.EqualTo(""));
				B.goInitialize.Set();
				Thread.Sleep(5);
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo("A, initializing ...\r\n\tB"));
				Assert.That(log.ToString(), Is.EqualTo("B.Initialize "));
				A.goInitialize.Set();
				Thread.Sleep(5);
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo("A\r\n\tB"));
				Assert.That(log.ToString(), Is.EqualTo("B.Initialize A.Initialize "));
				t.Wait();
			}
		}
		
		public class DisplayDisposingStatus : ConstructionLogTest
		{
			private static readonly StringBuilder log = new StringBuilder();

			public class A : IDisposable
			{
				public readonly B b;
				public static ManualResetEventSlim go = new ManualResetEventSlim();

				public A(B b)
				{
					this.b = b;
				}

				public void Dispose()
				{
					go.Wait();
					log.Append("A.Dispose ");
				}
			}

			public class B: IDisposable
			{
				public static ManualResetEventSlim go = new ManualResetEventSlim();

				public void Dispose()
				{
					go.Wait();
					log.Append("B.Dispose ");
				}
			}

			protected override void TearDown()
			{
				A.go.Set();
				B.go.Set();
				base.TearDown();
			}

			[Test]
			public void Test()
			{
				var container = Container();
				container.Get<A>();
				var t = Task.Run(() => container.Dispose());
				Thread.Sleep(15);
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo("A, disposing ...\r\n\tB"));
				Assert.That(log.ToString(), Is.EqualTo(""));
				A.go.Set();
				Thread.Sleep(5);
				Assert.That(container.Resolve<A>().GetConstructionLog(), Is.EqualTo("A\r\n\tB, disposing ..."));
				Assert.That(log.ToString(), Is.EqualTo("A.Dispose "));
				B.go.Set();
				Thread.Sleep(5);
				Assert.Throws<ObjectDisposedException>(() => container.Resolve<A>());
				t.Wait();
			}
		}

		public class IgnoreNonReadableProperties : ConstructionLogTest
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

		public class CycleSpanningContainerDependency : ConstructionLogTest
		{
			public class A
			{
				public A(IContainer container)
				{
					container.Get<B>();
				}
			}

			public class B
			{
				public readonly A a;

				public B(A a)
				{
					this.a = a;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var exception = Assert.Throws<SimpleContainerException>(() => container.Get<A>());
				Assert.That(exception.Message, Is.EqualTo("cyclic dependency for service [A], stack\r\n\tA\r\n\tB\r\n\tA\r\n\r\n!A\r\n\tIContainer\r\n\t!() => B\r\n\t\t!A"));
				Assert.That(exception.InnerException, Is.Null);
			}
		}


		public class DontTryToDumpResource : ConstructionLogTest
		{
			public class ServiceWithResource
			{
				public string streamContent;

				public ServiceWithResource([FromResource("testResource.txt")] Stream stream)
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
				Assert.That(() =>{ log = service.GetConstructionLog(); }, Throws.Nothing);
				Assert.That(log, Is.StringContaining("resource [testResource.txt]"));
			}
		}
	}
}