namespace SimpleContainer.Implementation
{
	internal class ServiceInstance
	{
		public object Instance { get; private set; }
		public ContainerService ContainerService { get; private set; }

		public ServiceInstance(object instance, ContainerService containerService)
		{
			Instance = instance;
			ContainerService = containerService;
		}
	}
}