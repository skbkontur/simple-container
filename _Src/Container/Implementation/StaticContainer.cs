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
		private readonly ConfigurationContext configurationContext;
		private readonly Action<Func<Type, bool>, ContainerConfigurationBuilder> fileConfigurator;

		public StaticContainer(IContainerConfiguration configuration, IInheritanceHierarchy inheritors,
			Func<AssemblyName, bool> assemblyFilter, ConfigurationContext configurationContext, ISet<Type> staticServices,
			Action<Func<Type, bool>, ContainerConfigurationBuilder> fileConfigurator)
			: base(configuration, inheritors, null, CacheLevel.Static, staticServices)
		{
			this.assemblyFilter = assemblyFilter;
			this.configurationContext = configurationContext;
			this.fileConfigurator = fileConfigurator;
		}

		public IContainer CreateLocalContainer(string name, Assembly primaryAssembly,
			Action<ContainerConfigurationBuilder> configure)
		{
			EnsureNotDisposed();
			var targetAssemblies = Utils.Closure(primaryAssembly, ReferencedAssemblies).ToSet();
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
			return new SimpleContainer(containerConfiguration, localHierarchy, this, CacheLevel.Local, staticServices);
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
	}
}