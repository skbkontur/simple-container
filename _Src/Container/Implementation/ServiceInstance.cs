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
	}
}