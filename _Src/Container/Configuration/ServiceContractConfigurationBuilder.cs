using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;

namespace SimpleContainer.Configuration
{
	public class ServiceContractConfigurationBuilder<T> :
		AbstractServiceConfigurationBuilder<ServiceContractConfigurationBuilder<T>, T>
	{
		internal ServiceContractConfigurationBuilder(ServiceConfigurationSet configurationSet, List<string> contracts)
			: base(configurationSet, contracts)
		{
		}

		public ServiceContractConfigurationBuilder<T> Contract<TContract>() where TContract : RequireContractAttribute, new()
		{
			return Contract(InternalHelpers.NameOf<TContract>());
		}

		public ServiceContractConfigurationBuilder<T> Contract(params string[] newContracts)
		{
			return new ServiceContractConfigurationBuilder<T>(configurationSet, contracts.Concat(newContracts.ToList()));
		}
	}
}