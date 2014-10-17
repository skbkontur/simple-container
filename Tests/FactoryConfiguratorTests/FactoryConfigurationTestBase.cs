using SimpleContainer.Tests.GenericsConfiguratorTests;

namespace SimpleContainer.Tests.FactoryConfiguratorTests
{
	public abstract class FactoryConfigurationTestBase : PreconfiguredContainerTestBase
	{
		protected override void Configure(ContainerConfigurationBuilder builder)
		{
			ApplyFactoriesConfigurator(builder);
		}
	}
}