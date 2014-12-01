using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;

namespace SimpleContainer.Implementation
{
	internal class StaticContainer : SimpleContainer, IStaticContainer
	{
		private readonly Func<AssemblyName, bool> assemblyFilter;
		private readonly Func<Type, object> settingsLoader;
		private IEnumerable<Type> staticServices;

		public StaticContainer(IContainerConfiguration configuration, IInheritanceHierarchy inheritors,
			Func<AssemblyName, bool> assemblyFilter,
			Func<Type, object> settingsLoader)
			: base(configuration, inheritors, null)
		{
			this.assemblyFilter = assemblyFilter;
			this.settingsLoader = settingsLoader;
		}

		public IContainer CreateLocalContainer(Assembly primaryAssembly, Action<ContainerConfigurationBuilder> configure)
		{
			var targetAssemblies = Utils.Closure(primaryAssembly, ReferencedAssemblies).ToSet();
			var restrictedHierarchy = new AssembliesRestrictedInheritanceHierarchy(targetAssemblies, inheritors);
			var configurationContext = new ConfigurationContext(settingsLoader, primaryAssembly);
			using (var configurationContainer = new SimpleContainer(configuration, restrictedHierarchy, this))
			{
				var invokers = configurationContainer.GetAll<IServiceConfiguratorInvoker>();
				foreach (var i in invokers)
					i.Configure(configurationContext);
				configurationContext.isLocalRun = true;
				foreach (var i in invokers)
					i.Configure(configurationContext);
			}
			if (configure != null)
				configure(configurationContext.Builder);
			var newStaticServices = configurationContext.Builder.GetStaticServices().ToArray();
			if (staticServices == null)
				staticServices = newStaticServices;
			else
			{
				var diff = staticServices.Except(newStaticServices).ToArray();
				if (diff.Any())
					throw new SimpleContainerException(string.Format("inconsistent static configuration, [{0}] were static, now local",
						diff.Select(x => x.FormatName()).JoinStrings(",")));
				diff = newStaticServices.Except(staticServices).ToArray();
				if (diff.Any())
					throw new SimpleContainerException(string.Format("inconsistent static configuration, [{0}] were local, now static",
						diff.Select(x => x.FormatName()).JoinStrings(",")));
			}
			var containerConfiguration = new MergedConfiguration(configuration, configurationContext.Builder.Build());
			return new SimpleContainer(containerConfiguration, restrictedHierarchy, this);
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

		private IEnumerable<Assembly> ReferencedAssemblies(Assembly assembly)
		{
			var referencedByAttribute = assembly.GetCustomAttributes<ContainerReferenceAttribute>()
				.Select(x => new AssemblyName(x.AssemblyName));
			return assembly.GetReferencedAssemblies()
				.Concat(referencedByAttribute)
				.Where(assemblyFilter)
				.Select(Assembly.Load);
		}

		public class ConfigurationContext
		{
			public Func<Type, object> SettingsLoader { get; private set; }
			private readonly Assembly primaryAssembly;
			public ContainerConfigurationBuilder Builder { get; private set; }
			public bool isLocalRun;

			public ConfigurationContext(Func<Type, object> settingsLoader, Assembly primaryAssembly)
			{
				SettingsLoader = settingsLoader;
				this.primaryAssembly = primaryAssembly;
				Builder = new ContainerConfigurationBuilder();
			}

			public IEnumerable<T> Filter<T>(IEnumerable<T> source)
			{
				return source.Where(x => IsLocal(x) == isLocalRun);
			}

			private bool IsLocal(object o)
			{
				return o.GetType().Assembly == primaryAssembly;
			}

			public TSettings GetSettings<TSettings>(Type configuratorType)
			{
				if (SettingsLoader == null)
				{
					const string messageFormat = "configurator [{0}] requires settings, but settings loader is not configured;" +
												 "configure it using ContainerFactory.SetSettingsLoader";
					throw new SimpleContainerException(string.Format(messageFormat, configuratorType.FormatName()));
				}
				var settingsInstance = SettingsLoader(typeof(TSettings));
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
						typeof(TSettings).FormatName(), settingsInstance.GetType().FormatName()));
				}
				return (TSettings) settingsInstance;
			}
		}
	}
}