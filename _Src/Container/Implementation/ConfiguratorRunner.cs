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
		private readonly Type[] priorities;

		private ConfiguratorRunner(ConfigurationContext context, IContainer container, Type[] priorities)
		{
			this.context = context;
			this.container = container;
			this.priorities = priorities;
		}

		public static ConfiguratorRunner Create(bool isStatic, IContainerConfiguration configuration,
			IInheritanceHierarchy hierarchy, ConfigurationContext context, Type[] priorities)
		{
			Func<Type, bool> filter = x => x.Assembly == containerAssembly || x.IsDefined<StaticAttribute>() == isStatic;
			var filteredHierarchy = new FilteredInheritanceHierarchy(hierarchy, filter);
			var container = new ConfigurationContainer(isStatic ? CacheLevel.Static : CacheLevel.Local,
				new FilteredContainerConfiguration(configuration, filter), filteredHierarchy);
			return new ConfiguratorRunner(context, container, priorities);
		}

		public void Run(ContainerConfigurationBuilder builder)
		{
			foreach (var invoker in container.GetAll<IServiceConfiguratorInvoker>())
				invoker.Configure(context, builder, priorities);
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
				var targetConfigurators = configurators
					.GroupBy(configurator => priorities == null
						? 0
						: configurator.GetType()
							.GetInterfaces()
							.Max(x => Array.IndexOf(priorities, x.GetDefinition())))
					.OrderByDescending(x => x.Key)
					.DefaultIfEmpty(Enumerable.Empty<IServiceConfigurator<T>>())
					.First();
				var serviceConfigurationBuilder = new ServiceConfigurationBuilder<T>(builder);
				foreach (var configurator in targetConfigurators)
					configurator.Configure(context, serviceConfigurationBuilder);
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