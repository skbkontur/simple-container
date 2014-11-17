namespace SimpleContainer.Tests.GenericsConfiguratorTests
{
	public abstract class PreconfiguredContainerTestBase : SimpleContainerTestBase
	{
		protected IContainer container;

		protected override void SetUp()
		{
			base.SetUp();
			container = Container();
		}
	}
}