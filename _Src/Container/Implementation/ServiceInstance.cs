using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class ServiceInstance
	{
		public ContainerService ContainerService { get; private set; }
		public object Instance { get; private set; }

		public ServiceInstance(object instance, ContainerService containerService)
		{
			ContainerService = containerService;
			Instance = instance;
		}

		public string FormatName()
		{
			var type = Instance == null ? ContainerService.Type : Instance.GetType();
			var name = new ServiceName(type, ContainerService.FinalUsedContracts);
			return name.FormatName();
		}
	}
}