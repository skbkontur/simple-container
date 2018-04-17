using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class DynamicConfigurationTest : SimpleContainerTestBase
	{
		public class CanUseIncludeDecisionsInImplementationSelectors : DynamicConfigurationTest
		{
			public interface IA
			{
			}

			public class AConfigurator : IContainerConfigurator
			{
				public void Configure(ConfigurationContext context, ContainerConfigurationBuilder builder)
				{
					builder.RegisterImplementationSelector(
						delegate(Type type, Type[] types, List<ImplementationSelectorDecision> decisions)
						{
							if (type == typeof (IA))
								decisions.Add(new ImplementationSelectorDecision
								{
									target = typeof (MyPrivateImpl),
									action = ImplementationSelectorDecision.Action.Include,
									comment = "private-impl"
								});
						});
				}

				private class MyPrivateImpl : IA
				{
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var resolved = container.Resolve<IA>();
				Assert.That(resolved.Single().GetType().Name, Is.EqualTo("MyPrivateImpl"));
				Assert.That(resolved.GetConstructionLog(), Is.EqualTo(TestHelpers.FormatMessage(@"
IA
	MyPrivateImpl - private-impl")));
			}
		}

		public class ImplementationFilters : DynamicConfigurationTest
		{
			public interface IA
			{
			}

			public class DefaultA : IA
			{
			}

			public class InMemoryA : IA
			{
			}

			public class LiteProfile : IProfile
			{
			}

			public class InMemoryProfile : IProfile
			{
			}

			public class InMemoryConventionConfigurator : IContainerConfigurator
			{
				public void Configure(ConfigurationContext context, ContainerConfigurationBuilder builder)
				{
					var selector = context.ProfileIs<InMemoryProfile>() ? GetSelector(true) : GetSelector(false);
					builder.RegisterImplementationSelector(selector);
				}

				private static ImplementationSelector GetSelector(bool inMemory)
				{
					return delegate(Type interfaceType, Type[] implementationTypes, List<ImplementationSelectorDecision> decisions)
					{
						if (interfaceType.IsInterface)
							foreach (var implementationType in implementationTypes)
								if (implementationType.Name.StartsWith("InMemory", StringComparison.OrdinalIgnoreCase) != inMemory)
									decisions.Add(new ImplementationSelectorDecision
									{
										action = ImplementationSelectorDecision.Action.Exclude,
										target = implementationType,
										comment = inMemory ? "in-memory" : "not-in-memory"
									});
					};
				}
			}

			[Test]
			public void Test()
			{
				using (var c = Factory().WithProfile(typeof (InMemoryProfile)).Build())
				{
					var resolved = c.Resolve<IA>();
					Assert.That(resolved.Single(), Is.InstanceOf<InMemoryA>());
					Assert.That(resolved.GetConstructionLog(), Is.EqualTo(TestHelpers.FormatMessage(@"
IA
	!DefaultA - in-memory
	InMemoryA")));
				}
				using (var c = Factory().WithProfile(typeof (LiteProfile)).Build())
				{
					var resolved = c.Resolve<IA>();
					Assert.That(resolved.Single(), Is.InstanceOf<DefaultA>());
					Assert.That(resolved.GetConstructionLog(), Is.EqualTo(TestHelpers.FormatMessage(@"
IA
	DefaultA
	!InMemoryA - not-in-memory")));
				}
			}
		}

		public class ImplementationFiltersCanBeOverriden : DynamicConfigurationTest
		{
			public interface IA
			{
			}

			public class DefaultA : IA
			{
			}

			public class InMemoryA : IA
			{
			}

			public class InMemoryConventionConfigurator : IContainerConfigurator
			{
				public void Configure(ConfigurationContext context, ContainerConfigurationBuilder builder)
				{
					builder.RegisterImplementationSelector(
						(interfaceType, implementationTypes, decisions) => decisions.Add(new ImplementationSelectorDecision
						{
							action = ImplementationSelectorDecision.Action.Exclude,
							target = typeof (DefaultA)
						}));
				}
			}

			public class AConfigurator : IServiceConfigurator<IA>
			{
				public void Configure(ConfigurationContext context, ServiceConfigurationBuilder<IA> builder)
				{
					builder.Bind<DefaultA>();
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				var resolved = container.Resolve<IA>();
				Assert.That(resolved.Single(), Is.InstanceOf<DefaultA>());
				Assert.That(resolved.GetConstructionLog(), Is.EqualTo(TestHelpers.FormatMessage(@"
IA
	DefaultA")));
			}
		}

		public class CanConfigureInheritors : DynamicConfigurationTest
		{
			public interface IRunnable
			{
			}

			public class A1 : IRunnable
			{
				public readonly int a;

				public A1(int a)
				{
					this.a = a;
				}
			}

			public class A2 : IRunnable
			{
				public readonly string b;

				public A2(string b)
				{
					this.b = b;
				}
			}

			public class RunnableConfigurator : IContainerConfigurator
			{
				public void Configure(ConfigurationContext context, ContainerConfigurationBuilder builder)
				{
					builder.InheritorsOf<IRunnable>("runnables use parameters", (t, b) => b.Dependencies(context.Parameters));
				}
			}

			[Test]
			public void Test()
			{
				var f = Factory().WithParameters(new TestingParametersSource(new Dictionary<string, object>
				{
					{"a", 42},
					{"b", "test string"},
				}));
				using (var container = f.Build())
				{
					var allRunnables = container.GetAll<IRunnable>().ToArray();
					Assert.That(allRunnables.OfType<A1>().Single().a, Is.EqualTo(42));
					Assert.That(allRunnables.OfType<A2>().Single().b, Is.EqualTo("test string"));
				}
			}
		}

		public class ForAllCrashHandledGracefully : DynamicConfigurationTest
		{
			[Test]
			public void Test()
			{
				Type crashThrownForType = null;
				var exception = Assert.Throws<SimpleContainerException>(() =>
					Container(b => b.ForAll("crash tester", delegate(Type t, ServiceConfigurationBuilder<object> _)
					{
						crashThrownForType = t;
						throw new InvalidOperationException("test crash");
					})));
				Assert.That(crashThrownForType, Is.Not.Null);
				var expectedMessage = string.Format("exception invoking [crash tester] for [{0}]", crashThrownForType.FormatName());
				Assert.That(exception.Message, Is.EqualTo(expectedMessage));
				Assert.That(exception.InnerException.Message, Is.EqualTo("test crash"));
			}
		}

		public class ApplyConfigurationByFilter : DynamicConfigurationTest
		{
			public class A1
			{
				public readonly int parameter;

				public A1(int parameter = 10)
				{
					this.parameter = parameter;
				}
			}

			public class A2
			{
				public readonly int parameter;

				public A2(int parameter = 10)
				{
					this.parameter = parameter;
				}
			}

			public class FilteredConfigurator : IContainerConfigurator
			{
				public void Configure(ConfigurationContext context, ContainerConfigurationBuilder builder)
				{
					builder.ForAll("spec dependency for A1", (t, b) =>
					{
						if (t == typeof (A1))
							b.Dependencies(new {parameter = 20});
					});
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<A1>().parameter, Is.EqualTo(20));
				Assert.That(container.Get<A2>().parameter, Is.EqualTo(10));
				Assert.That(container.Resolve<A1>().GetConstructionLog(),
					Is.EqualTo(TestHelpers.FormatMessage(@"
A1 - spec dependency for A1
	parameter -> 20")));
				Assert.That(container.Resolve<A2>().GetConstructionLog(),
					Is.EqualTo(TestHelpers.FormatMessage(@"
A2
	parameter -> 10")));
			}
		}
	}
}