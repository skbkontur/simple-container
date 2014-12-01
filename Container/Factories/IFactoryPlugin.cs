using SimpleContainer.Implementation;

namespace SimpleContainer.Factories
{
	internal interface IFactoryPlugin
	{
		bool TryInstantiate(IContainer container, ContainerService containerService);
	}
}