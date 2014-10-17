using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;

namespace SimpleContainer.Tests
{
	public abstract class BasicSimpleContainerTest : SimpleContainerTestBase
	{
		public class BindInvalidDependency_CorrectException : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container(x => x.BindDependency<A>("x", 42));
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<Wrap>());
				Assert.That(error.Message,
					Is.StringContaining("can't cast [Int32] to [String] for dependency [x] with value [42]\r\nWrap!\r\n\tA!"));
			}

			public class A
			{
				public readonly string x;

				public A(string x)
				{
					this.x = x;
				}
			}

			public class Wrap
			{
				public Wrap(A a)
				{
				}
			}
		}

		public class BindInvalidParameterValueOfSimpleType_CorrentExceptionAtConfigurationPhase : BasicSimpleContainerTest
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

		public class BindInvalidParameterValue_CorrentExceptionInConfigurationPhase : BasicSimpleContainerTest
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

		public class BindToInvalidImplementation_CorrectExceptionAtConfigurationPhase : BasicSimpleContainerTest
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

		public class CanConfigureGenericDefinition : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container(c =>
				{
					ApplyGenericsConfigurator(c);
					c.BindDependencyValue(typeof (GenericDefinition<>), typeof (int), 42);
				});
				var outerServices = container.GetAll<IOuterService>().ToArray();
				Assert.That(outerServices.Length, Is.EqualTo(2));
				Assert.That(outerServices.Single(x => x.ServiceType == typeof (ServiceA)).Argument, Is.EqualTo(42));
				Assert.That(outerServices.Single(x => x.ServiceType == typeof (ServiceB)).Argument, Is.EqualTo(42));
			}

			public class GenericDefinition<T> : IOuterService
				where T : IRestriction
			{
				public GenericDefinition(int argument, T service)
				{
					Argument = argument;
					Service = service;
				}

				public T Service { get; private set; }

				public int Argument { get; private set; }

				public Type ServiceType
				{
					get { return typeof (T); }
				}
			}

			public interface IOuterService
			{
				int Argument { get; }
				Type ServiceType { get; }
			}

			public interface IRestriction
			{
			}

			public class ServiceA : IRestriction
			{
			}

			public class ServiceB : IRestriction
			{
			}
		}

		public class CanInjectBaseNotAbstractClass : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(typeof (BaseClass), Is.EqualTo(container.Get<Service>().TypeOfMember));
			}

			public class BaseClass
			{
			}

			public class DerivedClass : BaseClass
			{
			}

			public class Service
			{
				private readonly BaseClass member;

				public Service(BaseClass member)
				{
					this.member = member;
				}

				public Type TypeOfMember
				{
					get { return member.GetType(); }
				}
			}
		}

		public class CanInjectEnumerableByAttribute : BasicSimpleContainerTest
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

		public class CanInjectEnumerableByCtor : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<OuterService>().Interfaces,
					Is.EquivalentTo(new IInterface[] {container.Get<Impl1>(), container.Get<Impl2>()}));
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

			public class OuterService
			{
				public OuterService(IEnumerable<IInterface> interfaces)
				{
					Interfaces = interfaces;
				}

				public IEnumerable<IInterface> Interfaces { get; private set; }
			}
		}

		public class CanInjectFactoriesOfGenerics : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var result = container.Get<SomeService<int>>().Factory();
				Assert.That(result, Is.Not.Null);
				Assert.That(result.GetType(), Is.EqualTo(typeof (ClassA<int>)));
			}

			public class ClassA<T>
			{
			}

			public class SomeService<T>
			{
				public SomeService(Func<ClassA<T>> factory)
				{
					Factory = factory;
				}

				public Func<ClassA<T>> Factory { get; set; }
			}
		}

		public class CanInjectGenericFactories : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container(ApplyFactoriesConfigurator);
				var instance = container.Get<SomeService>().Factory(typeof (int), new {argument = 42});
				Assert.That(instance.Type, Is.EqualTo(typeof (int)));
				Assert.That(instance.Argument, Is.EqualTo(42));
			}

			public interface ISomeInterface
			{
				int Argument { get; }
				Type Type { get; }
			}

			public class SomeService
			{
				public SomeService(Func<Type, object, ISomeInterface> factory)
				{
					Factory = factory;
				}

				public Func<Type, object, ISomeInterface> Factory { get; private set; }

				private class SomeGenericService<T> : ISomeInterface
				{
					public SomeGenericService(int argument)
					{
						Argument = argument;
					}

					public int Argument { get; private set; }

					public Type Type
					{
						get { return typeof (T); }
					}
				}
			}
		}

		public class CanInjectGenericService : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container(ApplyGenericsConfigurator);
				var result = container.Get<SomeService<int>>();
				Assert.That(result.otherService, Is.SameAs(container.Get<OtherService<int>>()));
			}

			public class OtherService<T>
			{
			}

			public class SomeService<T>
			{
				public SomeService(OtherService<T> otherService)
				{
					this.otherService = otherService;
				}

				public OtherService<T> otherService { get; private set; }
			}
		}

		public class CanInjectResource : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var service = container.Get<ServiceWithResource>();
				Assert.That(service.GetContent(), Is.EqualTo("the resource"));
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<ServiceA>());
				Assert.That(error.Message,
					Is.StringContaining(
						"can't find resource [inexistent.txt] in namespace of [SimpleContainer.Tests.BasicSimpleContainerTest+CanInjectResource+ServiceWithInexistentResource], assembly [SimpleContainer.Tests]\r\nServiceA!\r\n\tServiceWithInexistentResource!"));
			}

			public class ServiceA
			{
				public ServiceA(ServiceWithInexistentResource serviceWithInexistentResource)
				{
				}
			}

			public class ServiceWithInexistentResource
			{
				public ServiceWithInexistentResource([FromResource("inexistent.txt")] Stream stream)
				{
				}
			}

			public class ServiceWithResource
			{
				public Stream stream;

				public ServiceWithResource([FromResource("testResource.txt")] Stream stream)
				{
					this.stream = stream;
				}

				public string GetContent()
				{
					return stream.ReadUtf8String();
				}
			}
		}

		public class CanInjectSimpleFactories : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container(ApplyFactoriesConfigurator);
				var result = container.Get<SomeService>().Factory();
				Assert.That(result, Is.Not.Null);
				Assert.That(result.GetType(), Is.EqualTo(typeof (ClassA)));
			}

			public class ClassA
			{
			}

			public class SomeService
			{
				public SomeService(Func<ClassA> factory)
				{
					Factory = factory;
				}

				public Func<ClassA> Factory { get; set; }
			}
		}

		public class CanResolveContainer : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<SomeService>(), Is.SameAs(container.Get<IContainer>().Get<SomeService>()));
			}

			public class SomeService
			{
			}
		}

		public class CannotCreateOpenGenerics : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<IInterface>());
				Assert.That(error.Message, Is.StringContaining("GenericClass<T>! - has open generic arguments"));
			}

			public class GenericClass<T> : IInterface
			{
			}

			public interface IInterface
			{
			}
		}

		//todo сомнительная фича, выпились при возможности

		public class CannotResolveServiceFactory_NoFallback : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<A>());
				Assert.That(error.Message, Is.StringContaining("Factory"));
			}

			private class A
			{
				public class Factory
				{
					public Factory(string unresolved)
					{
					}

					public A Create()
					{
						return new A();
					}
				}
			}
		}

		public class ContainerConstructorAttribute : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<Service>().CtorName, Is.EqualTo("second"));
			}

			public class Service
			{
				public Service(string someValue)
				{
					CtorName = "first";
				}

				[Infection.ContainerConstructor]
				public Service()
				{
					CtorName = "second";
				}

				public string CtorName { get; private set; }
			}
		}

		public class CrashDuringConstruction_WrapWithContainerException : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var exception = Assert.Throws<SimpleContainerException>(() => container.Get<OuterService>());
				var messageText = exception.ToString();
				Assert.That(messageText, Is.StringContaining("construction exception\r\nOuterService!\r\n\tInnerService!\r\n"));
				Assert.That(messageText, Is.StringContaining("test crash"));
				Assert.That(messageText, Is.StringContaining("InvalidOperationException"));
			}

			public class InnerService
			{
				public InnerService()
				{
					throw new InvalidOperationException("test crash");
				}
			}

			public class OuterService
			{
				public OuterService(InnerService innerService)
				{
					InnerService = innerService;
				}

				public InnerService InnerService { get; private set; }
			}
		}

		public class CyclicDependency : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<OuterService>());
				Assert.That(error.Message,
					Is.EqualTo(
						"cyclic dependency ClassA ...-> ClassB -> ClassA\r\nOuterService!\r\n\tClassA!\r\n\t\tIntermediate!\r\n\t\t\tClassB!\r\n\t\t\t\tClassA!"));
			}

			public class ClassA
			{
				public ClassA(Intermediate intermediate)
				{
				}
			}

			public class ClassB
			{
				public ClassB(ClassA classA)
				{
				}
			}

			public class Intermediate
			{
				public Intermediate(ClassB classB)
				{
				}
			}

			public class OuterService
			{
				public OuterService(ClassA classA)
				{
				}
			}
		}

		public class DependencyConfiguredWithNull : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container(x => x.BindDependency<Impl>("someInterface", null));
				Assert.That(container.Get<Impl>().SomeInterface, Is.Null);
			}

			public interface ISomeInterface
			{
			}

			public class Impl
			{
				public Impl(ISomeInterface someInterface)
				{
					SomeInterface = someInterface;
				}

				public ISomeInterface SomeInterface { get; private set; }
			}
		}

		public class DependencyWithOptionalValue : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<Impl>().Parameter, Is.EqualTo(42));
			}

			public class Impl
			{
				public Impl(int parameter = 42)
				{
					Parameter = parameter;
				}

				public int Parameter { get; private set; }
			}
		}

		public class DontReuseAttributeWorks : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<ClassA>(), Is.Not.SameAs(container.Get<ClassA>()));
				Assert.That(container.Get<ClassB>(), Is.Not.SameAs(container.Get<ClassB>()));
				Assert.That(container.Get<IInterface>(), Is.Not.SameAs(container.Get<IInterface>()));
				Assert.That(container.Get<Impl>(), Is.SameAs(container.Get<Impl>()));
			}

			[DontReuse]
			public class ClassA
			{
			}

			public class ClassB : ClassA
			{
			}

			[DontReuse]
			public interface IInterface
			{
			}

			public class Impl : IInterface
			{
			}
		}

		public class DontUsePluggable : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container(c => c.DontUsePluggable(typeof (Impl1)));
				Assert.That(container.Get<IInterface>(), Is.SameAs(container.Get<Impl2>()));
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

		public class DontUsePluggableIsTakenIntoAccountWhenDetectingImplementations : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container(c => c.DontUsePluggable(typeof (B)));
				Assert.That(container.GetImplementationsOf<IIntf>(), Is.EquivalentTo(new[] {typeof (A)}));
			}

			public class A : IIntf
			{
			}

			public class B : IIntf
			{
			}

			public interface IIntf
			{
			}
		}

		public class EnumerableDependecyImplementationNotCreatedDueToHostMismatch : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container(x => x.WithHostName("h1"));
				Assert.That(container.Get<A>().interfaces, Is.Empty);
			}

			public class A
			{
				public readonly IEnumerable<IInterface> interfaces;

				public A(IEnumerable<IInterface> interfaces)
				{
					this.interfaces = interfaces;
				}
			}

			[Hosting("h2")]
			public class B : IInterface
			{
			}

			public interface IInterface
			{
			}
		}

		public class EnumerableDependenciesAreRequired : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<A>());
				Assert.That(error.Message, Is.StringContaining("A!\r\n\tB!\r\n\t\tunresolved!"));
			}

			public class A
			{
				public A(IEnumerable<B> dependency)
				{
				}
			}

			public class B
			{
				public B(string unresolved)
				{
				}
			}
		}

		public class ErrorMessageForGenerics : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container(c => c.Bind(typeof (IInterface), typeof (SomeImpl<int>)),
					c => c.Bind(typeof (IInterface), typeof (SomeImpl<string>)));
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<IInterface>());
				Assert.That(error.Message,
					Is.EqualTo(
						"many implementations for IInterface\r\n\tSomeImpl<Int32>\r\n\tSomeImpl<String>\r\nIInterface++\r\n\tSomeImpl<Int32>\r\n\tSomeImpl<String>"));
			}

			public interface IInterface
			{
			}

			public class SomeImpl<T> : IInterface
			{
			}
		}

		public class FactoryDependantOnServiceType : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container(builder => builder.Bind<ChildService>(c => new ChildService(c.Target)));
				Assert.That(container.Get<SomeService>().ChildService.ParentService, Is.EqualTo(typeof (SomeService)));
				Assert.That(container.Get<ChildService>().ParentService, Is.Null);
			}

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
		}

		public class FilterByHost : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container(c => c.WithHostName("h2"));
				Assert.That(container.GetAll<IIntf>(),
					Is.EquivalentTo(new IIntf[] {container.Get<B>(), container.Get<C>(), container.Get<D>()}));
				Assert.That(container.GetImplementationsOf<IIntf>(), Is.EquivalentTo(new[] {typeof (B), typeof (C), typeof (D)}));

				Assert.That(container.GetConstructionLog(typeof (IIntf)),
					Is.StringContaining("IIntf++\r\n\tA! - host mismatch, declared h1 != current h2\r\n\tB\r\n\tC"));
			}

			[Hosting("h1")]
			public class A : IIntf
			{
			}

			public class B : IIntf
			{
			}

			[Hosting("h2")]
			public class C : IIntf
			{
			}

			[Hosting("h1", "h2")]
			public class D : IIntf
			{
			}

			public interface IIntf
			{
			}
		}

		public class GetAfterGetAll_CorrectErrorMessage : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.GetAll<IInterface>().Count(), Is.EqualTo(2));
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<IInterface>());
				Assert.That(error.Message,
					Is.EqualTo("many implementations for IInterface\r\n\tImpl1\r\n\tImpl2\r\nIInterface++\r\n\tImpl1\r\n\tImpl2"));
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

		public class GracefullErrorMessageForUncreatableTypes : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				const string message = "can't create simple type\r\nSomeClass!\r\n\targument! - <---------------";
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<SomeClass>());
				Assert.That(error.Message, Is.EqualTo(message));
			}

			public class SomeClass
			{
				public SomeClass(int argument)
				{
					Argument = argument;
				}

				public int Argument { get; private set; }
			}
		}

		public class GracefullErrorMessageWhenNoImplementationFound : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				const string message =
					"no implementations for OuterOuterService\r\nOuterOuterService!\r\n\tOuterService!\r\n\t\tIInterface!";
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<OuterOuterService>());
				Assert.That(error.Message, Is.EqualTo(message));
			}

			public interface IInterface
			{
			}

			public class OuterOuterService
			{
				public OuterOuterService(OuterService outerService)
				{
					OuterService = outerService;
				}

				public OuterService OuterService { get; private set; }
			}

			public class OuterService
			{
				public OuterService(IInterface @interface)
				{
					Interface = @interface;
				}

				public IInterface Interface { get; private set; }
			}
		}

		public class HostNotSet_AllAccepted : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.GetAll<IIntf>(),
					Is.EquivalentTo(new IIntf[] {container.Get<A>(), container.Get<B>(), container.Get<C>()}));
				Assert.That(container.GetImplementationsOf<IIntf>(), Is.EquivalentTo(new[] {typeof (A), typeof (B), typeof (C)}));
			}

			[Hosting("h1")]
			public class A : IIntf
			{
			}

			public class B : IIntf
			{
			}

			[Hosting("h2")]
			public class C : IIntf
			{
			}

			public interface IIntf
			{
			}
		}

		public class IgnoreImplementation : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<IInterface>(), Is.InstanceOf<Impl1>());
			}

			public interface IInterface
			{
			}

			public class Impl1 : IInterface
			{
			}

			[IgnoreImplementation]
			public class Impl2 : IInterface
			{
			}
		}

		public class ImplementationWithDependencyConfiguredByType : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container(c => c.BindDependency<Impl, int>(42));
				Assert.That(container.Get<Impl>().Parameter, Is.EqualTo(42));
			}

			public class Impl
			{
				public Impl(int parameter)
				{
					Parameter = parameter;
				}

				public int Parameter { get; private set; }
			}
		}

		public class ImplementationWithDependencyImplementationConfig : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container(c => c.BindDependencyImplementation<Impl, Impl2>("someInterface"));
				Assert.That(container.Get<Impl>().SomeInterface, Is.SameAs(container.Get<Impl2>()));
			}

			public interface ISomeInterface
			{
			}

			public class Impl
			{
				public Impl(ISomeInterface someInterface)
				{
					SomeInterface = someInterface;
				}

				public ISomeInterface SomeInterface { get; private set; }
			}

			public class Impl1 : ISomeInterface
			{
			}

			public class Impl2 : ISomeInterface
			{
			}
		}

		public class ImplementationWithExplicitDelegateFactory : BasicSimpleContainerTest
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

		public class ImplementationWithValueConfig : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container(c => c.BindDependency<Impl>("someValue", 42));
				Assert.That(container.Get<Impl>().SomeValue, Is.EqualTo(42));
			}

			public class Impl
			{
				public Impl(int someValue)
				{
					SomeValue = someValue;
				}

				public int SomeValue { get; private set; }
			}
		}

		public class InterfaceWithConstantImplementation : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var impl = new Impl();
				var container = Container(x => x.Bind<ISomeInterface>(impl));
				Assert.That(container.Get<ISomeInterface>(), Is.SameAs(impl));
			}

			public interface ISomeInterface
			{
			}

			public class Impl : ISomeInterface
			{
			}
		}

		public class MultipleImplementations_CorrectError : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				const string message =
					"many implementations for IInterface\r\n\tImpl1\r\n\tImpl2\r\nIInterface++\r\n\tImpl1\r\n\tImpl2";
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<IInterface>());
				Assert.That(error.Message, Is.EqualTo(message));
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

		public class NotCreatableService_Throw : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				const string message =
					"can't create simple type\r\nOuterService!\r\n\tIInterface!\r\n\t\tImpl1!\r\n\t\t\tparameter! - <---------------";
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<OuterService>());
				Assert.That(error.Message, Is.EqualTo(message));
			}

			public interface IInterface
			{
			}

			public class Impl1 : IInterface
			{
				public Impl1(int parameter)
				{
					Parameter = parameter;
				}

				public int Parameter { get; private set; }
			}

			public class Impl2 : IInterface
			{
			}

			public class OuterService
			{
				public OuterService(IInterface iInterface)
				{
					Interface = iInterface;
				}

				public IInterface Interface { get; set; }
			}
		}

		public class ReportManyCtors : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<IInterface>());
				Assert.That(error.Message,
					Is.StringContaining(
						"many public ctors, maybe some of them should be made private?\r\nIInterface!\r\n\tTestService! - <---------------"));
			}

			public interface IInterface
			{
			}

			public class SomeClass
			{
			}

			public class TestService : IInterface
			{
				public TestService()
					: this(null)
				{
				}

				public TestService(SomeClass someClass)
				{
				}
			}
		}

		public class ReportSkipMessageForPrivateCtors : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<IInterface>());
				Assert.That(error.Message,
					Is.StringContaining("no public ctors, maybe ctor is private?\r\nIInterface!\r\n\tTestService! - <---------------"));
			}

			public interface IInterface
			{
			}

			public class TestService : IInterface
			{
				private TestService()
				{
				}
			}
		}

		public class ServiceFactory : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<SomeService>().otherService, Is.SameAs(container.Get<SomeOtherService>()));
			}

			public class SomeOtherService
			{
			}

			public class SomeService
			{
				public readonly SomeOtherService otherService;

				private SomeService(SomeOtherService otherService)
				{
					this.otherService = otherService;
				}

				public class Factory
				{
					private readonly SomeOtherService otherService;

					public Factory(SomeOtherService otherService)
					{
						this.otherService = otherService;
					}

					public SomeService Create()
					{
						return new SomeService(otherService);
					}
				}
			}
		}

		public class ServiceWithConfig : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container(c => c.Bind<ISomeInterface, Impl1>());
				Assert.That(container.Get<ISomeInterface>(), Is.SameAs(container.Get<Impl1>()));
			}

			public interface ISomeInterface
			{
			}

			public class Impl1 : ISomeInterface
			{
			}

			public class Impl2 : ISomeInterface
			{
			}
		}

		public class ServiceWithDependency : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<SomeService>().SomeOtherService, Is.SameAs(container.Get<SomeOtherService>()));
			}

			public class SomeOtherService
			{
			}

			public class SomeService
			{
				public SomeService(SomeOtherService someOtherService)
				{
					SomeOtherService = someOtherService;
				}

				public SomeOtherService SomeOtherService { get; private set; }
			}
		}

		public class ServiceWithMultipleImplementations : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.GetAll<AbstractParent>(),
					Is.EquivalentTo(new AbstractParent[] {container.Get<Child1>(), container.Get<Child2>()}));
			}

			public abstract class AbstractParent
			{
			}

			public class Child1 : AbstractParent
			{
			}

			public class Child2 : AbstractParent
			{
			}
		}

		public class SetConfiguration : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				container.SetConfiguration(x => x.Bind<IIntf, B>());
				Assert.That(container.Get<IIntf>(), Is.SameAs(container.Get<B>()));
			}

			public class A : IIntf
			{
			}

			public class B : IIntf
			{
			}

			public interface IIntf
			{
			}
		}

		public class Simple : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<SomeService>(), Is.SameAs(container.Get<SomeService>()));
			}

			public class SomeService
			{
			}
		}

		public class SimpleWithInterface : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<ISomeServiceInterface>(), Is.SameAs(container.Get<ISomeServiceInterface>()));
			}

			public interface ISomeServiceInterface
			{
			}

			public class SomeService : ISomeServiceInterface
			{
			}
		}

		//todo по неабстактному родителю зарезолвить все реализации, в том числе его самого ???

		public class SimpleWithParent : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<AbstractParent>(), Is.SameAs(container.Get<AbstractParent>()));
				Assert.That(container.Get<AbstractParent>(), Is.SameAs(container.Get<Child>()));
			}

			public abstract class AbstractParent
			{
			}

			public class Child : AbstractParent
			{
			}
		}

		public class SkipNestedPrivate : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var outerService = container.Get<OuterService>().Interfaces.ToArray();
				Assert.That(outerService.Single(), Is.InstanceOf<OuterPublicService>());
			}

			public interface IInterface
			{
			}

			private class InnerPrivateService : IInterface
			{
			}

			public class OuterPublicService : IInterface
			{
			}

			public class OuterService
			{
				public OuterService(IEnumerable<IInterface> interfaces)
				{
					Interfaces = interfaces;
				}

				public IEnumerable<IInterface> Interfaces { get; private set; }
			}
		}

		public class StructsCannotBeCreated : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<StructConsumer>());
				Assert.That(error.Message,
					Is.StringContaining(string.Format(
						"can't create value type\r\nStructConsumer!\r\n\tSomeStruct! - <---------------")));
				error = Assert.Throws<SimpleContainerException>(() => container.Get<ITestInterface>());
				Assert.That(error.Message,
					Is.StringContaining(string.Format("ITestInterface!\r\n\tSomeStruct! - <---------------")));
			}

			public interface ITestInterface
			{
			}

			public struct SomeStruct : ITestInterface
			{
			}

			public class SomeClass : ITestInterface
			{
			}

			public class StructConsumer
			{
				public readonly SomeStruct someStruct;

				public StructConsumer(SomeStruct someStruct)
				{
					this.someStruct = someStruct;
				}
			}
		}

		public class TrackDependenciesForUnresolvedServices : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				Assert.Throws<SimpleContainerException>(() => container.GetAll<ClassA>());
				var childContainer = Container(c => c.BindDependency<ClassB>("argument", "42"));
				Assert.That(childContainer.Get<ClassA>().ClassB.Argument, Is.EqualTo("42"));
			}

			public class ClassA
			{
				public ClassA(ClassB classB)
				{
					ClassB = classB;
				}

				public ClassB ClassB { get; set; }
			}

			public class ClassB
			{
				public ClassB(string argument)
				{
					Argument = argument;
				}

				public string Argument { get; set; }
			}
		}

		public class UseConstructorArgumentNamesInErrorMessageForSimpleTypesOnly : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container = Container();
				const string message =
					"can't create simple type\r\nOuterService!\r\n\tIInterface!\r\n\t\tChild1!\r\n\t\t\targument! - <---------------";
				var error = Assert.Throws<SimpleContainerException>(() => container.Get<OuterService>());
				Assert.That(error.Message, Is.EqualTo(message));

				const string message2 = "no implementations for Child2\r\nChild2!\r\n\tIOtherService!";
				error = Assert.Throws<SimpleContainerException>(() => container.Get<Child2>());
				Assert.That(error.Message, Is.EqualTo(message2));
			}

			public class Child1 : IInterface
			{
				public Child1(int argument)
				{
					Argument = argument;
				}

				public int Argument { get; private set; }
			}

			public class Child2 : IInterface
			{
				public Child2(IOtherService otherService)
				{
					OtherService = otherService;
				}

				public IOtherService OtherService { get; private set; }
			}

			public interface IInterface
			{
			}

			public interface IOtherService
			{
			}

			public class OuterService
			{
				public OuterService(IInterface someService)
				{
					SomeService = someService;
				}

				public IInterface SomeService { get; private set; }
			}
		}

		public class UseValueCreatedBy : BasicSimpleContainerTest
		{
			[Test]
			public void Test()
			{
				var container =
					Container(c => c.BindDependencyFactory<SomeService>("serviceA", x => new ServiceA(42, x.Get<ServiceB>())));
				var serviceA = container.Get<SomeService>().ServiceA;
				Assert.That(serviceA.Argument, Is.EqualTo(42));
				Assert.That(serviceA.ServiceB, Is.Not.Null);
			}

			public class ServiceA
			{
				public ServiceA(int argument, ServiceB serviceB)
				{
					Argument = argument;
					ServiceB = serviceB;
				}

				public int Argument { get; private set; }
				public ServiceB ServiceB { get; private set; }
			}

			public class ServiceB
			{
			}

			public class SomeService
			{
				public SomeService(ServiceA serviceA)
				{
					ServiceA = serviceA;
				}

				public ServiceA ServiceA { get; private set; }
			}
		}
	}
}