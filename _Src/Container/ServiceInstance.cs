using SimpleContainer.Helpers;
using SimpleContainer.Implementation;

namespace SimpleContainer
{
	internal class ServiceInstance
	{
		private readonly ContainerService containerService;
		public object Instance { get; private set; }

		public ServiceInstance(object instance, ContainerService containerService)
		{
			this.containerService = containerService;
			Instance = instance;
		}

		public string FormatName()
		{
			var result = Instance.GetType().FormatName();
			var usedContracts = InternalHelpers.FormatContractsKey(containerService.FinalUsedContracts);
			if (!string.IsNullOrEmpty(usedContracts))
				result += "[" + usedContracts + "]";
			return result;
		}
	}
}