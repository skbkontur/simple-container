using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;

namespace SimpleContainer.Configuration
{
	public class ServiceConfigurationBuilder<T> :
		AbstractServiceConfigurationBuilder<ServiceConfigurationBuilder<T>, T>
	{
		private readonly StaticServicesConfigurator staticServices;

		internal ServiceConfigurationBuilder(ServiceConfigurationSet configurationSet,
			StaticServicesConfigurator staticServices)
			: base(configurationSet, new List<string>())
		{
			this.staticServices = staticServices;
		}

		public ServiceContractConfigurationBuilder<T> Contract<TContract>() where TContract : RequireContractAttribute, new()
		{
			return Contract(InternalHelpers.NameOf<TContract>());
		}

		public ServiceContractConfigurationBuilder<T> Contract(params string[] newContracts)
		{
			return new ServiceContractConfigurationBuilder<T>(configurationSet, contracts.Concat(newContracts.ToList()));
		}

		public ServiceConfigurationBuilder<T> MakeStatic()
		{
			staticServices.MakeStatic(typeof(T));
			return this;
		}
	}
}