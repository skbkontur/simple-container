using System;
using System.Collections.Generic;
using System.Reflection;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	internal class StaticContainer : SimpleContainer, IStaticContainer
	{
		private readonly Func<AssemblyName, bool> assemblyFilter;
		private readonly ConfigurationContext configurationContext;
		private readonly Action<Func<Type, bool>, ContainerConfigurationBuilder> fileConfigurator;

		public StaticContainer(IContainerConfiguration configuration, IInheritanceHierarchy inheritors,
			Func<AssemblyName, bool> assemblyFilter, ConfigurationContext configurationContext, ISet<Type> staticServices,
			Action<Func<Type, bool>, ContainerConfigurationBuilder> fileConfigurator, LogError logError, LogInfo logInfo)
			: base(configuration, inheritors, null, CacheLevel.Static, staticServices, logError, logInfo)
		{
			this.assemblyFilter = assemblyFilter;
			this.configurationContext = configurationContext;
			this.fileConfigurator = fileConfigurator;
		}

		public IContainer CreateLocalContainer(string name, Assembly primaryAssembly,
			Action<ContainerConfigurationBuilder> configure)
		{
			EnsureNotDisposed();
			var targetAssemblies = Utils.Closure(primaryAssembly, x => x.ReferencedAssemblies(assemblyFilter)).ToSet();
			Func<Type, bool> filter = x => targetAssemblies.Contains(x.Assembly);
			var localHierarchy = new FilteredInheritanceHierarchy(inheritors, filter);
			var builder = new ContainerConfigurationBuilder(staticServices, false);
			var localContext = configurationContext.Local(name, primaryAssembly);
			using (var runner = ConfiguratorRunner.Create(false, configuration, localHierarchy, localContext))
			{
				runner.Run(builder, c => c.GetType().Assembly != primaryAssembly);
				runner.Run(builder, c => c.GetType().Assembly == primaryAssembly);
			}
			if (configure != null)
				configure(builder);
			if (fileConfigurator != null)
				fileConfigurator(filter, builder);
			var containerConfiguration = new MergedConfiguration(configuration, builder.Build());
			return new SimpleContainer(containerConfiguration, localHierarchy, this, CacheLevel.Local,
				staticServices, errorLogger, infoLogger);
		}

		public new IStaticContainer Clone(Action<ContainerConfigurationBuilder> configure)
		{
			EnsureNotDisposed();
			return new StaticContainer(CloneConfiguration(configure), inheritors, assemblyFilter,
				configurationContext, staticServices, fileConfigurator, errorLogger, infoLogger);
		}
	}
}