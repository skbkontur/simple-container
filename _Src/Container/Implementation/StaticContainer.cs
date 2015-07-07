using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class StaticContainer : SimpleContainer, IStaticContainer
	{
		private readonly Func<AssemblyName, bool> assemblyFilter;
		private readonly ConfigurationContext configurationContext;
		private readonly Action<Func<Type, bool>, ContainerConfigurationBuilder> fileConfigurator;
		private readonly List<Assembly> pluginAssemblies;
		private readonly GenericsAutoCloser genericsAutoCloser;

		public StaticContainer(IContainerConfiguration configuration, IInheritanceHierarchy inheritors,
			Func<AssemblyName, bool> assemblyFilter, ConfigurationContext configurationContext, ISet<Type> staticServices,
			Action<Func<Type, bool>, ContainerConfigurationBuilder> fileConfigurator, LogError logError, LogInfo logInfo,
			List<Assembly> pluginAssemblies, GenericsAutoCloser genericsAutoCloser)
			: base(configuration, inheritors, null, CacheLevel.Static, staticServices, logError, logInfo, genericsAutoCloser)
		{
			this.assemblyFilter = assemblyFilter;
			this.configurationContext = configurationContext;
			this.fileConfigurator = fileConfigurator;
			this.pluginAssemblies = pluginAssemblies;
			this.genericsAutoCloser = genericsAutoCloser;
		}

		public IContainer CreateLocalContainer(string name, Assembly primaryAssembly,
			IParametersSource parameters, Action<ContainerConfigurationBuilder> configure)
		{
			EnsureNotDisposed();

			var localHierarchy = inheritors;
			var builder = new ContainerConfigurationBuilder(staticServices, false);
			var localContext = configurationContext.Local(name, primaryAssembly, parameters);
			using (var runner = ConfiguratorRunner.Create(false, configuration, localHierarchy, localContext,genericsAutoCloser))
			{
				runner.Run(builder, c => c.GetType().GetTypeInfo().Assembly != primaryAssembly);
				runner.Run(builder, c => c.GetType().GetTypeInfo().Assembly == primaryAssembly);
			}
			if (configure != null)
				configure(builder);
			var containerConfiguration = new MergedConfiguration(configuration, builder.Build());
			return new SimpleContainer(containerConfiguration, localHierarchy, this, CacheLevel.Local,
				staticServices, errorLogger, infoLogger,genericsAutoCloser);
		}

		public new IStaticContainer Clone(Action<ContainerConfigurationBuilder> configure)
		{
			EnsureNotDisposed();
			return new StaticContainer(CloneConfiguration(configure), inheritors, assemblyFilter,
				configurationContext, staticServices, fileConfigurator, null, infoLogger, pluginAssemblies,genericsAutoCloser);
		}
	}
}