using SimpleContainer.Implementation;

namespace SimpleContainer.Factories
{
	public interface IFactoryPlugin
	{
		bool TryInstantiate(IContainer container, ContainerService containerService);
	}
}