using SimpleContainer.Implementation;

namespace SimpleContainer.Factories
{
	internal interface IFactoryPlugin
	{
		bool TryInstantiate(ContainerService.Builder builder);
	}
}