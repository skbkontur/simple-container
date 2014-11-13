namespace SimpleContainer.Factories
{
	public interface IFactoryPlugin
	{
		bool TryInstantiate(IServiceFactory serviceFactory, ContainerService containerService);
	}
}