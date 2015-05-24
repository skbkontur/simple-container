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
		private readonly Type[] priorities;

		public StaticContainer(IConfigurationRegistry configurationRegistry, IInheritanceHierarchy inheritors,
			Func<AssemblyName, bool> assemblyFilter, ConfigurationContext configurationContext, ISet<Type> staticServices,
			Action<Func<Type, bool>, ContainerConfigurationBuilder> fileConfigurator, LogError logError, LogInfo logInfo,
			List<Assembly> pluginAssemblies, Type[] priorities)
			: base(configurationRegistry, inheritors, null, CacheLevel.Static, staticServices, logError, logInfo)
		{
			this.assemblyFilter = assemblyFilter;
			this.configurationContext = configurationContext;
			this.fileConfigurator = fileConfigurator;
			this.pluginAssemblies = pluginAssemblies;
			this.priorities = priorities;
		}

		public IContainer CreateLocalContainer(string name, Assembly primaryAssembly,
			IParametersSource parameters, Action<ContainerConfigurationBuilder> configure)
		{
			EnsureNotDisposed();

			var targetAssemblies = EnumerableHelpers.Return(primaryAssembly).Closure(assemblyFilter).Concat(pluginAssemblies).ToSet();
			Func<Type, bool> filter = x => targetAssemblies.Contains(x.Assembly);
			var localHierarchy = new FilteredInheritanceHierarchy(inheritors, filter);
			var builder = new ContainerConfigurationBuilder(staticServices, false);
			var localContext = configurationContext.Local(name, parameters);
			using (var runner = ConfiguratorRunner.Create(false, Configuration, localHierarchy, localContext, priorities))
				runner.Run(builder);
			if (configure != null)
				configure(builder);
			if (fileConfigurator != null)
				fileConfigurator(filter, builder);
			var containerConfiguration = new MergedConfiguration(Configuration, builder.RegistryBuilder.Build());
			return new SimpleContainer(containerConfiguration, localHierarchy, this, CacheLevel.Local, staticServices,
				errorLogger, infoLogger);
		}

		public new IStaticContainer Clone(Action<ContainerConfigurationBuilder> configure)
		{
			EnsureNotDisposed();
			return new StaticContainer(CloneConfiguration(configure), inheritors, assemblyFilter,
				configurationContext, staticServices, fileConfigurator, null, infoLogger, pluginAssemblies, priorities);
		}
	}
}