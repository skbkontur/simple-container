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
		private readonly Func<Type, object> settingsLoader;
		private readonly IContainer container;

		private ConfiguratorRunner(Func<Type, object> settingsLoader, IContainer container)
		{
			this.settingsLoader = settingsLoader;
			this.container = container;
		}

		public static ConfiguratorRunner Create(bool isStatic, IContainerConfiguration configuration,
			IInheritanceHierarchy hierarchy, Func<Type, object> settingsLoader)
		{
			var thisAssembly = Assembly.GetExecutingAssembly();
			Func<Type, bool> filter = x => x.Assembly == thisAssembly || x.IsDefined<StaticAttribute>() == isStatic;
			var staticHierarchy = new FilteredInheritanceHierarchy(hierarchy, filter);
			var container = new ConfigurationContainer(isStatic ? CacheLevel.Static : CacheLevel.Local,
				new FilteredContainerConfiguration(configuration, filter), staticHierarchy);
			return new ConfiguratorRunner(settingsLoader, container);
		}

		public void Run(ContainerConfigurationBuilder builder, Func<object, bool> configuratorsFilter)
		{
			var configurationContext = new ConfigurationContext(settingsLoader, configuratorsFilter, builder);
			foreach (var invoker in container.GetAll<IServiceConfiguratorInvoker>())
				invoker.Configure(configurationContext);
		}

		public void Dispose()
		{
			container.Dispose();
		}

		private class ConfigurationContainer : SimpleContainer
		{
			public ConfigurationContainer(CacheLevel cacheLevel, IContainerConfiguration configuration,
				IInheritanceHierarchy inheritors)
				: base(configuration, inheritors, null, cacheLevel)
			{
			}

			internal override CacheLevel GetCacheLevel(Type type)
			{
				return cacheLevel;
			}
		}

		public interface IServiceConfiguratorInvoker
		{
			void Configure(ConfigurationContext context);
		}

		public class ServiceConfiguratorInvoker<T> : IServiceConfiguratorInvoker
		{
			private readonly IEnumerable<IServiceConfigurator<T>> configurators;

			public ServiceConfiguratorInvoker(IEnumerable<IServiceConfigurator<T>> configurators)
			{
				this.configurators = configurators;
			}

			public void Configure(ConfigurationContext context)
			{
				var serviceConfigurationBuilder = new ServiceConfigurationBuilder<T>(context.Builder);
				foreach (var configurator in context.Filter(configurators))
					configurator.Configure(serviceConfigurationBuilder);
			}
		}

		public class ServiceConfiguratorWithSettingsInvoker<TSettings, T> : IServiceConfiguratorInvoker
		{
			private readonly IEnumerable<IServiceConfigurator<TSettings, T>> configurators;

			public ServiceConfiguratorWithSettingsInvoker(IEnumerable<IServiceConfigurator<TSettings, T>> configurators)
			{
				this.configurators = configurators;
			}

			public void Configure(ConfigurationContext context)
			{
				var serviceConfigurationBuilder = new ServiceConfigurationBuilder<T>(context.Builder);
				foreach (var c in context.Filter(configurators))
					c.Configure(context.GetSettings<TSettings>(c.GetType()), serviceConfigurationBuilder);
			}
		}

		public class ContainerConfiguratorInvoker : IServiceConfiguratorInvoker
		{
			private readonly IEnumerable<IContainerConfigurator> configurators;

			public ContainerConfiguratorInvoker(IEnumerable<IContainerConfigurator> configurators)
			{
				this.configurators = configurators;
			}

			public void Configure(ConfigurationContext context)
			{
				foreach (var c in context.Filter(configurators))
					c.Configure(context.Builder);
			}
		}

		public class ContainerConfiguratorWithSettingsInvoker<TSettings> : IServiceConfiguratorInvoker
		{
			private readonly IEnumerable<IContainerConfigurator<TSettings>> configurators;

			public ContainerConfiguratorWithSettingsInvoker(IEnumerable<IContainerConfigurator<TSettings>> configurators)
			{
				this.configurators = configurators;
			}

			public void Configure(ConfigurationContext context)
			{
				foreach (var c in context.Filter(configurators))
					c.Configure(context.GetSettings<TSettings>(c.GetType()), context.Builder);
			}
		}

		public class ConfigurationContext
		{
			private readonly Func<object, bool> filter;
			public Func<Type, object> SettingsLoader { get; private set; }
			public ContainerConfigurationBuilder Builder { get; private set; }

			public ConfigurationContext(Func<Type, object> settingsLoader, Func<object, bool> filter,
				ContainerConfigurationBuilder builder)
			{
				this.filter = filter;
				SettingsLoader = settingsLoader;
				Builder = builder;
			}

			public IEnumerable<T> Filter<T>(IEnumerable<T> source)
			{
				return source.Where(x => filter(x));
			}

			public TSettings GetSettings<TSettings>(Type configuratorType)
			{
				if (SettingsLoader == null)
				{
					const string messageFormat = "configurator [{0}] requires settings, but settings loader is not configured;" +
					                             "configure it using ContainerFactory.SetSettingsLoader";
					throw new SimpleContainerException(string.Format(messageFormat, configuratorType.FormatName()));
				}
				var settingsInstance = SettingsLoader(typeof (TSettings));
				if (settingsInstance == null)
				{
					const string messageFormat = "configurator [{0}] requires settings, but settings loader returned null";
					throw new SimpleContainerException(string.Format(messageFormat, configuratorType.FormatName()));
				}
				if (settingsInstance is TSettings == false)
				{
					const string messageFormat = "configurator [{0}] requires settings [{1}], " +
					                             "but settings loader returned [{2}]";
					throw new SimpleContainerException(string.Format(messageFormat, configuratorType.FormatName(),
						typeof (TSettings).FormatName(), settingsInstance.GetType().FormatName()));
				}
				return (TSettings) settingsInstance;
			}
		}
	}
}