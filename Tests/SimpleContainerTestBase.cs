using System;
using System.Collections.Specialized;
using System.Reflection;
using SimpleContainer.Configuration;
using SimpleContainer.Factories;
using SimpleContainer.Generics;

namespace SimpleContainer.Tests
{
	public abstract class SimpleContainerTestBase : UnitTestBase
	{
		protected SimpleContainer Container(params Action<ContainerConfigurationBuilder>[] configure)
		{
			var targetTypes = GetType().GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public);
			return new SimpleContainer(targetTypes, configure);
		}

		protected void ApplyFactoriesConfigurator(ContainerConfigurationBuilder builder)
		{
			UseProfileConfigurator(new FactoryConfigurator(), builder);
		}

		protected void ApplyGenericsConfigurator(ContainerConfigurationBuilder builder)
		{
			UseProfileConfigurator(new GenericsConfigurator(x => x.Name.StartsWith("SimpleContainer")), builder);
		}

		private static void UseProfileConfigurator(IHandleProfile<BasicProfile> profileConfigurator,
			ContainerConfigurationBuilder builder)
		{
			profileConfigurator.Handle(new NameValueCollection(), builder);
		}
	}
}