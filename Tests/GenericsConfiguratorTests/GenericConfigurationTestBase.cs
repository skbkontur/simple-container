namespace SimpleContainer.Tests.GenericsConfiguratorTests
{
	public abstract class GenericConfigurationTestBase : PreconfiguredContainerTestBase
	{
		protected override void Configure(ContainerConfigurationBuilder builder)
		{
			ApplyGenericsConfigurator(builder);
		}
	}
}