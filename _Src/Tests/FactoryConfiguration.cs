using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class FactoryConfiguration: SimpleContainerTestBase
	{
		public class ExplicitDelegateFactoryWithEnumerableInjection : FactoryConfiguration
		{
			public class A
			{
				public readonly IEnumerable<IX> instances;

				public A(IEnumerable<IX> instances)
				{
					this.instances = instances;
				}
			}

			public interface IX
			{
			}

			public class X : IX
			{
				public readonly Type target;

				public X(Type target)
				{
					this.target = target;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.Bind<IX>((c, t) => new X(t)));
				Assert.That(container.Get<A>().instances.Cast<X>().Single().target, Is.EqualTo(typeof(A)));
			}
		}

		public class FactoryDependantOnServiceType : FactoryConfiguration
		{
			public class ChildService
			{
				public ChildService(Type parentService)
				{
					ParentService = parentService;
				}

				public Type ParentService { get; private set; }
			}

			public class SomeService
			{
				public SomeService(ChildService childService)
				{
					ChildService = childService;
				}

				public ChildService ChildService { get; private set; }
			}

			[Test]
			public void Test()
			{
				var container = Container(b => b.Bind((c, t) => new ChildService(t)));
				Assert.That(container.Get<SomeService>().ChildService.ParentService, Is.EqualTo(typeof (SomeService)));
				Assert.That(container.Get<ChildService>().ParentService, Is.Null);
			}
		}

		public class FactoryWithServiceNotOwnByContainer : FactoryConfiguration
		{
			[Test]
			public void Test()
			{
				var container = Container(b => b.Bind(c => new A(), false));
				container.Get<A>();
				container.Dispose();
				Assert.That(A.disposeCallCount, Is.EqualTo(0));
			}

			public class A : IDisposable
			{
				public static int disposeCallCount;

				public void Dispose()
				{
					disposeCallCount++;
				}
			}
		}

		public class FactoryDependantOnServiceTypeCalledOnlyOnceForEachTarget : FactoryConfiguration
		{
			[Test]
			public void Test()
			{
				var callLog = new StringBuilder();
				var container = Container(b => b.Bind((c, t) =>
				{
					callLog.AppendFormat("create for {0} ", t.Name);
					return new A(t);
				}));
				var createTarget1 = container.Get<Func<Target1>>();
				var createTarget2 = container.Get<Func<Target2>>();
				Assert.That(callLog.ToString(), Is.EqualTo(""));

				Assert.That(createTarget1().a.Target, Is.EqualTo(typeof(Target1)));
				Assert.That(callLog.ToString(), Is.EqualTo("create for Target1 "));
				Assert.That(createTarget1().a.Target, Is.EqualTo(typeof(Target1)));
				Assert.That(callLog.ToString(), Is.EqualTo("create for Target1 "));

				Assert.That(createTarget2().a.Target, Is.EqualTo(typeof(Target2)));
				Assert.That(callLog.ToString(), Is.EqualTo("create for Target1 create for Target2 "));
				Assert.That(createTarget2().a.Target, Is.EqualTo(typeof(Target2)));
				Assert.That(callLog.ToString(), Is.EqualTo("create for Target1 create for Target2 "));
			}

			public class Target1
			{
				public readonly A a;

				public Target1(A a)
				{
					this.a = a;
				}
			}

			public class Target2
			{
				public readonly A a;

				public Target2(A a)
				{
					this.a = a;
				}
			}

			public class A
			{
				public Type Target { get; set; }

				public A(Type target)
				{
					Target = target;
				}
			}
		}

		public class CanInjectStructViaExplicitConfiguration : FactoryConfiguration
		{
			public class A
			{
				public readonly Token token;

				public A(Token token)
				{
					this.token = token;
				}
			}

			public struct Token
			{
				public int value;
			}

			public class TokenSource
			{
				public Token CreateToken()
				{
					return new Token { value = 78 };
				}
			}

			public class TokenConfigurator : IServiceConfigurator<Token>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<Token> builder)
				{
					builder.Bind(c => c.Get<TokenSource>().CreateToken());
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<A>().token.value, Is.EqualTo(78));
			}
		}

		public class CanBindFactoryViaServiceConfigurator : FactoryConfiguration
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
				public readonly Type ownerType;

				public B(Type ownerType)
				{
					this.ownerType = ownerType;
				}
			}

			public class AConfigurator : IServiceConfigurator<B>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<B> builder)
				{
					builder.Bind((c, t) => new B(t));
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var a = container.Get<A>();
				Assert.That(a.b.ownerType, Is.EqualTo(typeof(A)));
			}
		}

		public class ImplementationWithExplicitDelegateFactory : FactoryConfiguration
		{
			[Test]
			public void Test()
			{
				var impl = new Impl();
				var container = Container(x => x.Bind<IInterface>(c => impl));
				Assert.That(container.Get<IInterface>(), Is.SameAs(impl));
			}

			public interface IInterface
			{
			}

			public class Impl : IInterface
			{
			}
		}

		public class LastConfigurationWins : FactoryConfiguration
		{
			public class A
			{
				public readonly B parameter;

				public A(B parameter)
				{
					this.parameter = parameter;
				}
			}

			public class B
			{
				public int value;
			}

			[Test]
			public void Test()
			{
				var container = Container(delegate(ContainerConfigurationBuilder b)
				{
					b.Bind(c => new B { value = 1 });
					b.Bind<B>(new B { value = 2 });
				});
				Assert.That(container.Get<A>().parameter.value, Is.EqualTo(2));
			}
		}

		public class HandleGracefullyFactoryCrash : FactoryConfiguration
		{
			[Test]
			public void Test()
			{
				var container = Container(b => b.Bind<A>(c => { throw new InvalidOperationException("my test crash"); }));
				
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<AWrap>());
				Assert.That(error.Message, Is.EqualTo("service [A] construction exception"
					+ Environment.NewLine
					+ Environment.NewLine + "!AWrap"
					+ Environment.NewLine + "\t!A <---------------"));
				Assert.That(error.InnerException, Is.InstanceOf<InvalidOperationException>());
				Assert.That(error.InnerException.Message, Is.EqualTo("my test crash"));
			}

			public class AWrap
			{
				public readonly A a;

				public AWrap(A a)
				{
					this.a = a;
				}
			}

			public class A
			{
			}
		}
		
		public class HandleGracefullyFactoryWithTargetCrash : FactoryConfiguration
		{
			[Test]
			public void Test()
			{
				var container = Container(b => b.Bind<A>((c, t) => { throw new InvalidOperationException("my test crash"); }));
				
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<AWrap>());
				Assert.That(error.Message, Is.EqualTo("service [A] construction exception"
					+ Environment.NewLine
					+ Environment.NewLine + "!AWrap"
					+ Environment.NewLine + "\t!A <---------------"));
				Assert.That(error.InnerException, Is.InstanceOf<InvalidOperationException>());
				Assert.That(error.InnerException.Message, Is.EqualTo("my test crash"));
			}

			public class AWrap
			{
				public readonly A a;

				public AWrap(A a)
				{
					this.a = a;
				}
			}

			public class A
			{
			}
		}
		
		public class HandleGracefullyDependencyFactoryCrash : FactoryConfiguration
		{
			[Test]
			public void Test()
			{
				var container = Container(b => b.BindDependencyFactory<AWrap>("a",
					_ => { throw new InvalidOperationException("my test crash"); }));

				var error = Assert.Throws<SimpleContainerException>(() => container.Get<AWrap>());
				Assert.That(error.Message, Is.EqualTo("service [A] construction exception"
					+ Environment.NewLine
					+ Environment.NewLine + "!AWrap"
					+ Environment.NewLine + "\t!a <---------------"));
				Assert.That(error.InnerException, Is.InstanceOf<InvalidOperationException>());
				Assert.That(error.InnerException.Message, Is.EqualTo("my test crash"));
			}

			public class AWrap
			{
				public readonly A a;

				public AWrap(A a)
				{
					this.a = a;
				}
			}

			public class A
			{
			}
		}
		
		public class CanRefuseToCreateServiceFromFactory : FactoryConfiguration
		{
			[Test]
			public void Test()
			{
				var container = Container(b => b.Bind<A>(c => { throw new ServiceCouldNotBeCreatedException("test refused"); }));

				var resolvedWrap = container.Resolve<AWrap>();
				Assert.That(resolvedWrap.Single().a, Is.Null);
				Assert.That(resolvedWrap.GetConstructionLog(), Is.EqualTo("AWrap"
					+ Environment.NewLine + "\tA - test refused -> <null>"));
			}

			public class AWrap
			{
				public readonly A a;

				public AWrap(A a = null)
				{
					this.a = a;
				}
			}

			public class A
			{
			}
		}
	}
}