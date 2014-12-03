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
		private readonly ISet<Type> staticServices;

		public StaticContainer(IContainerConfiguration configuration, IInheritanceHierarchy inheritors,
			Func<AssemblyName, bool> assemblyFilter, Func<Type, object> settingsLoader, ISet<Type> staticServices)
			: base(configuration, inheritors, null, CacheLevel.Static)
		{
			this.assemblyFilter = assemblyFilter;
			this.settingsLoader = settingsLoader;
			this.staticServices = staticServices;
		}

		internal override CacheLevel GetCacheLevel(Type type)
		{
			return staticServices.Contains(type) || type.IsDefined<StaticAttribute>() ? CacheLevel.Static : CacheLevel.Local;
		}

		public IContainer CreateLocalContainer(Assembly primaryAssembly, Action<ContainerConfigurationBuilder> configure)
		{
			EnsureNotDisposed();
			var targetAssemblies = Utils.Closure(primaryAssembly, ReferencedAssemblies).ToSet();
			var localHierarchy = new FilteredInheritanceHierarchy(inheritors, x => targetAssemblies.Contains(x.Assembly));

			var builder = new ContainerConfigurationBuilder(staticServices, false);
			using (var runner = ConfiguratorRunner.Create(false, configuration, localHierarchy, settingsLoader))
			{
				runner.Run(builder, c => c.GetType().Assembly != primaryAssembly);
				runner.Run(builder, c => c.GetType().Assembly == primaryAssembly);
			}
			if (configure != null)
				configure(builder);
			var containerConfiguration = new MergedConfiguration(configuration, builder.Build());
			return new SimpleContainer(containerConfiguration, localHierarchy, this, CacheLevel.Local);
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