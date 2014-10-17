namespace SimpleContainer.Factories
{
	public interface IFactoryPlugin
	{
		bool TryInstantiate(ContainerService containerService);
	}
}