using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;

namespace SimpleContainer.Implementation
{
	internal class ConfiguratorRunner : IDisposable
	{
		private static readonly Assembly containerAssembly = typeof (ConfiguratorRunner).Assembly;
		private readonly ConfigurationContext context;
		private readonly IContainer container;

		private ConfiguratorRunner(ConfigurationContext context, IContainer container)
		{
			this.context = context;
			this.container = container;
		}

		public static ConfiguratorRunner Create(bool isStatic, IContainerConfiguration configuration,
			IInheritanceHierarchy hierarchy, ConfigurationContext context)
		{
			Func<Type, bool> filter = x => x.Assembly == containerAssembly || x.IsDefined<StaticAttribute>() == isStatic;
			var filteredHierarchy = new FilteredInheritanceHierarchy(hierarchy, filter);
			var container = new ConfigurationContainer(isStatic ? CacheLevel.Static : CacheLevel.Local,
				new FilteredContainerConfiguration(configuration, filter), filteredHierarchy);
			return new ConfiguratorRunner(context, container);
		}

		public void Run(ContainerConfigurationBuilder builder)
		{
			foreach (var invoker in container.GetAll<IServiceConfiguratorInvoker>())
				invoker.Configure(context, builder);
		}

		public void Dispose()
		{
			container.Dispose();
		}

		private class ConfigurationContainer : SimpleContainer
		{
			public ConfigurationContainer(CacheLevel cacheLevel, IContainerConfiguration configuration,
				IInheritanceHierarchy inheritors)
				: base(configuration, inheritors, null, cacheLevel, new HashSet<Type>(), null, null)
			{
			}

			internal override CacheLevel GetCacheLevel(Type type)
			{
				return cacheLevel;
			}
		}

		internal interface IServiceConfiguratorInvoker
		{
			void Configure(ConfigurationContext context, ContainerConfigurationBuilder builder);
		}

		internal class ServiceConfiguratorInvoker<T> : IServiceConfiguratorInvoker
		{
			private readonly IEnumerable<IServiceConfigurator<T>> configurators;

			public ServiceConfiguratorInvoker(IEnumerable<IServiceConfigurator<T>> configurators)
			{
				this.configurators = configurators.GroupBy(GetPriority)
					.OrderByDescending(x => x.Key)
					.DefaultIfEmpty(Enumerable.Empty<IServiceConfigurator<T>>())
					.First();
			}

			public void Configure(ConfigurationContext context, ContainerConfigurationBuilder builder)
			{
				var serviceConfigurationBuilder = new ServiceConfigurationBuilder<T>(builder);
				foreach (var configurator in configurators)
					configurator.Configure(context, serviceConfigurationBuilder);
			}

			private static int GetPriority(IServiceConfigurator<T> configurator)
			{
				if (configurator is IHighPriorityServiceConfigurator<T>)
					return 3;
				if (configurator is IMediumPriorityServiceConfigurator<T>)
					return 2;
				return 1;
			}
		}

		internal class ContainerConfiguratorInvoker : IServiceConfiguratorInvoker
		{
			private readonly IEnumerable<IContainerConfigurator> configurators;

			public ContainerConfiguratorInvoker(IEnumerable<IContainerConfigurator> configurators)
			{
				this.configurators = configurators;
			}

			public void Configure(ConfigurationContext context, ContainerConfigurationBuilder builder)
			{
				foreach (var c in configurators)
					c.Configure(context, builder);
			}
		}
	}
}