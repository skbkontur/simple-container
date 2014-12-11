using SimpleContainer.Implementation;

namespace SimpleContainer.Factories
{
	internal interface IFactoryPlugin
	{
		bool TryInstantiate(Implementation.SimpleContainer container, ContainerService containerService);
	}
}