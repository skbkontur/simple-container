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

		public void Run(ContainerConfigurationBuilder builder, Func<object, bool> configuratorsFilter)
		{
			var internalContext = new ConfigurationContextInternal(context, builder, configuratorsFilter);
			foreach (var invoker in container.GetAll<IServiceConfiguratorInvoker>())
				invoker.Configure(internalContext);
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
			void Configure(ConfigurationContextInternal internalContext);
		}

		internal class ServiceConfiguratorInvoker<T> : IServiceConfiguratorInvoker
		{
			private readonly IEnumerable<IServiceConfigurator<T>> configurators;

			public ServiceConfiguratorInvoker(IEnumerable<IServiceConfigurator<T>> configurators)
			{
				this.configurators = configurators;
			}

			public void Configure(ConfigurationContextInternal internalContext)
			{
				var serviceConfigurationBuilder = new ServiceConfigurationBuilder<T>(internalContext.Builder);
				foreach (var configurator in internalContext.FilterConfigurators(configurators))
					configurator.Configure(internalContext.Context, serviceConfigurationBuilder);
			}
		}

		internal class ContainerConfiguratorInvoker : IServiceConfiguratorInvoker
		{
			private readonly IEnumerable<IContainerConfigurator> configurators;

			public ContainerConfiguratorInvoker(IEnumerable<IContainerConfigurator> configurators)
			{
				this.configurators = configurators;
			}

			public void Configure(ConfigurationContextInternal internalContext)
			{
				foreach (var c in internalContext.FilterConfigurators(configurators))
					c.Configure(internalContext.Context, internalContext.Builder);
			}
		}

		internal class ConfigurationContextInternal
		{
			public ConfigurationContext Context { get; private set; }
			public ContainerConfigurationBuilder Builder { get; private set; }
			private readonly Func<object, bool> configuratorsFilter;

			public ConfigurationContextInternal(ConfigurationContext context, ContainerConfigurationBuilder builder,
				Func<object, bool> configuratorsFilter)
			{
				Context = context;
				Builder = builder;
				this.configuratorsFilter = configuratorsFilter;
			}

			public IEnumerable<TConfigurator> FilterConfigurators<TConfigurator>(IEnumerable<TConfigurator> configurators)
			{
				return configurators.Where(x => configuratorsFilter(x));
			}
		}
	}
}