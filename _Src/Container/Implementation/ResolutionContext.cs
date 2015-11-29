using System.Collections.Generic;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class ResolutionContext
	{
		public SimpleContainer container;

		public List<ContainerService.Builder> stack = new List<ContainerService.Builder>();
		public HashSet<ServiceName> constructingServices = new HashSet<ServiceName>();
		public ContractsList contracts = new ContractsList();
		public bool analizeDependenciesOnly;
	}
}