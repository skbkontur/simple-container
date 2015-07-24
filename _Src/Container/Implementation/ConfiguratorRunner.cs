using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	internal class ConfiguratorRunner
	{
		private readonly IEnumerable<IServiceConfiguratorInvoker> invokers;

		public ConfiguratorRunner(IEnumerable<IServiceConfiguratorInvoker> invokers)
		{
			this.invokers = invokers;
		}

		public void Run(ContainerConfigurationBuilder builder, ConfigurationContext context, Type[] priorities)
		{
			foreach (var invoker in invokers)
				invoker.Configure(context, builder, priorities);
		}

		internal interface IServiceConfiguratorInvoker
		{
			void Configure(ConfigurationContext context, ContainerConfigurationBuilder builder, Type[] priorities);
		}

		internal class ServiceConfiguratorInvoker<T> : IServiceConfiguratorInvoker
		{
			private readonly IEnumerable<IServiceConfigurator<T>> configurators;

			public ServiceConfiguratorInvoker(IEnumerable<IServiceConfigurator<T>> configurators)
			{
				this.configurators = configurators;
			}

			public void Configure(ConfigurationContext context, ContainerConfigurationBuilder builder, Type[] priorities)
			{
				var configurationSet = builder.RegistryBuilder.GetConfigurationSet(typeof (T));
				Action action = delegate
				{
					var serviceConfigurationBuilder = new ServiceConfigurationBuilder<T>(configurationSet);
					var targetConfigurators = configurators
						.GroupBy(configurator => priorities == null
							? 0
							: GetLeafInterfaces(configurator).Max(x => Array.IndexOf(priorities, x.GetDefinition())))
						.Where(x => x.Key >= 0)
						.OrderByDescending(x => x.Key)
						.DefaultIfEmpty(Enumerable.Empty<IServiceConfigurator<T>>())
						.First();
					foreach (var configurator in targetConfigurators)
					{
						try
						{
							configurator.Configure(context, serviceConfigurationBuilder);
						}
						catch (Exception e)
						{
							const string messageFormat = "error executing configurator [{0}]";
							configurationSet.SetError(string.Format(messageFormat, configurator.GetType().FormatName()), e);
							return;
						}
					}
				};
				configurationSet.RegisterLazyConfigurator(action);
			}

			private static Type[] GetLeafInterfaces(IServiceConfigurator<T> configurator)
			{
				var interfaces = configurator.GetType().GetInterfaces();
				var parents = interfaces.SelectMany(i => i.GetInterfaces());
				return interfaces.Except(parents).ToArray();
			}
		}

		internal class ContainerConfiguratorInvoker : IServiceConfiguratorInvoker
		{
			private readonly IEnumerable<IContainerConfigurator> configurators;

			public ContainerConfiguratorInvoker(IEnumerable<IContainerConfigurator> configurators)
			{
				this.configurators = configurators;
			}

			public void Configure(ConfigurationContext context, ContainerConfigurationBuilder builder, Type[] priorities)
			{
				foreach (var c in configurators)
					c.Configure(context, builder);
			}
		}
	}
}