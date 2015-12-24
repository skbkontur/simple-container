using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SimpleContainer.Annotations;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.Factories
{
	public abstract class CtorDelegatesTest : SimpleContainerTestBase
	{
		public class Simple : CtorDelegatesTest
		{
			public class A
			{
				public delegate A Ctor();
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var factory = container.Get<A.Ctor>();
				Assert.That(factory(), Is.Not.Null);
				Assert.That(factory(), Is.Not.SameAs(factory()));
			}
		}

		public class CanChooseCtorByDelegateArgumentTypes : CtorDelegatesTest
		{
			public class A
			{
				public readonly int a;
				public readonly string b;
				public readonly C c;

				[UsedImplicitly]
				private A(C c, int a)
				{
					this.c = c;
					this.a = a;
				}

				[UsedImplicitly]
				private A(string b, C c)
				{
					this.b = b;
					this.c = c;
				}

				public delegate A ByInt(int a);

				public delegate A ByString(string b);
			}

			public class C
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();

				var byIntFactory = container.Get<A.ByInt>();
				var instance1 = byIntFactory(42);
				Assert.That(instance1.a, Is.EqualTo(42));
				Assert.That(instance1.b, Is.Null);
				Assert.That(instance1.c, Is.Not.Null);

				var byStringFactory = container.Get<A.ByString>();
				var instance2 = byStringFactory("test-string");
				Assert.That(instance2.a, Is.EqualTo(0));
				Assert.That(instance2.b, Is.EqualTo("test-string"));
				Assert.That(instance2.c, Is.Not.Null);
			}
		}

		public class CanChooseCtorWithNotExactEqualParameterType : CtorDelegatesTest
		{
			public class A
			{
				public readonly int a;
				public readonly IX x;

				public A(int a, IX x)
				{
					this.a = a;
					this.x = x;
				}

				public A(int a, X2 x)
				{
					this.a = a;
					this.x = x;
				}

				public delegate A Ctor(X1 x, int a);
			}

			public interface IX
			{
			}

			public class X1 : IX
			{
			}

			public class X2 : X1
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var factory = container.Get<A.Ctor>();
				var x1 = new X1();
				var a = factory(x1, 42);
				Assert.That(a.x, Is.SameAs(x1));
				Assert.That(a.a, Is.EqualTo(42));
			}
		}

		public class SingleParameter : CtorDelegatesTest
		{
			public class A
			{
				public readonly int p;

				public A(int p)
				{
					this.p = p;
				}

				public delegate A Ctor(int p);
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var factory = container.Get<A.Ctor>();
				Assert.That(factory(42).p, Is.EqualTo(42));
			}
		}

		public class ManyParameters : CtorDelegatesTest
		{
			public class A
			{
				public readonly int p0;
				public readonly int p1;
				public readonly int p2;
				public readonly int p3;
				public readonly int p4;

				public A(int p0, int p1, int p2, int p3, int p4)
				{
					this.p0 = p0;
					this.p1 = p1;
					this.p2 = p2;
					this.p3 = p3;
					this.p4 = p4;
				}

				public delegate A Ctor(int p0, int p1, int p2, int p3, int p4);
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var factory = container.Get<A.Ctor>();
				var instance = factory(1, 2, 3, 4, 5);
				Assert.That(instance.p0, Is.EqualTo(1));
				Assert.That(instance.p1, Is.EqualTo(2));
				Assert.That(instance.p2, Is.EqualTo(3));
				Assert.That(instance.p3, Is.EqualTo(4));
				Assert.That(instance.p4, Is.EqualTo(5));
			}
		}

		public class ConstructorWithServices : CtorDelegatesTest
		{
			public class A
			{
				public readonly B b;
				public readonly int p;

				public A(B b, int p)
				{
					this.b = b;
					this.p = p;
				}

				public delegate A Ctor(int p);
			}

			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var ctor = container.Get<A.Ctor>();
				var instance = ctor(42);
				Assert.That(instance.b, Is.SameAs(container.Get<B>()));
				Assert.That(instance.p, Is.EqualTo(42));
			}
		}

		public class ServiceWithBadDependency : CtorDelegatesTest
		{
			public interface IA
			{
			}

			public class B
			{
				public readonly IA a;

				public B(IA a)
				{
					this.a = a;
				}

				public delegate B Ctor();
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var exception = Assert.Throws<SimpleContainerException>(() => container.Get<B.Ctor>());
				Assert.That(exception.Message,
					Is.EqualTo("no instances for [B] because [IA] has no instances\r\n\r\n!B.Ctor\r\n\t!IA - has no implementations" +
					           defaultScannedAssemblies));
			}
		}

		public class ServiceWithPrivateCtor : CtorDelegatesTest
		{
			public class A
			{
				public readonly int a;

				private A()
					: this(42)
				{
				}

				public A(int a)
				{
					this.a = a;
				}

				public delegate A Default();
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var factory = container.Get<A.Default>();
				Assert.That(factory().a, Is.EqualTo(42));
			}
		}

		public class CtorHasNotBoundParameters : CtorDelegatesTest
		{
			public class A
			{
				public readonly int p1;

				public A(int p1, string q)
				{
					this.p1 = p1;
				}

				public delegate A Ctor();
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var exception = Assert.Throws<SimpleContainerException>(() => container.Get<A.Ctor>());
				Assert.That(exception.Message, Is.EqualTo("can't find matching ctor\r\n\r\n!A.Ctor <---------------"));
			}
		}

		public class DelegateHasNotUsedParameters : CtorDelegatesTest
		{
			public class A
			{
				public delegate A Ctor(int p1, string p2);
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var exception = Assert.Throws<SimpleContainerException>(() => container.Get<A.Ctor>());
				Assert.That(exception.Message,
					Is.EqualTo("delegate has not used parameters [p1,p2]\r\n\r\n!A.Ctor <---------------"));
			}
		}

		public class InvalidDelegateParameterType : CtorDelegatesTest
		{
			public class A
			{
				public readonly int testParameter;

				public A(int testParameter)
				{
					this.testParameter = testParameter;
				}

				public delegate A Ctor(string testParameter);
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var exception = Assert.Throws<SimpleContainerException>(() => container.Get<A.Ctor>());
				Assert.That(exception.Message, Is.EqualTo("can't find matching ctor\r\n\r\n!A.Ctor <---------------"));
			}
		}

		public class SkipDelegatesWithNotMatchingReturnType : CtorDelegatesTest
		{
			public class A
			{
				public delegate B Ctor();
			}

			public class B
			{
				public int b;
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var exception = Assert.Throws<SimpleContainerException>(() => container.Get<A.Ctor>());
				Assert.That(exception.Message, Is.EqualTo("can't create delegate [A.Ctor]. return type must match declaring\r\n\r\n!A.Ctor <---------------"));
			}
		}

		public class SkipPrivateDelegates : CtorDelegatesTest
		{
			public class A
			{
				private delegate A Ctor();
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var ctorType = typeof (A).GetNestedTypes(BindingFlags.NonPublic).Single(x => x.Name == "Ctor");
				var exception = Assert.Throws<SimpleContainerException>(() => container.Get(ctorType));
				Assert.That(exception.Message, Is.EqualTo("can't create delegate [A.Ctor]. must be nested public\r\n\r\n!A.Ctor <---------------"));
			}
		}

		public class CtorDeleateWithServiceNameInConstructor : CtorDelegatesTest
		{
			public class A
			{
				public readonly ServiceName name1;
				public readonly long a;
				public readonly ServiceName name2;

				public A(ServiceName name1, int a, ServiceName name2)
				{
					this.name1 = name1;
					this.a = a;
					this.name2 = name2;
				}

				public delegate A Ctor(int a);
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var factory = container.Get<A.Ctor>();
				var instance = factory(42);
				Assert.That(instance.a, Is.EqualTo(42));
				Assert.That(instance.name1.Type, Is.EqualTo(typeof (A)));
				Assert.That(instance.name2.Type, Is.EqualTo(typeof (A)));
			}
		}

		public class SkipNonNestedDelegate : CtorDelegatesTest
		{
			public class Service
			{
				public int Value { get; set; }

				public Service(int value)
				{
					Value = value;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var exception = Assert.Throws<SimpleContainerException>(() => container.Get<ServiceCtor>());
				Assert.That(exception.Message, Is.EqualTo("can't create delegate [ServiceCtor]. must be nested public\r\n\r\n!ServiceCtor <---------------"));
			}
		}
	}

	public delegate CtorDelegatesTest.SkipNonNestedDelegate.Service ServiceCtor(int value);
}