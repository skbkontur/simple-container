using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Helpers;

namespace SimpleContainer.Hosting
{
	public class HostingEnvironment
	{
		private readonly IInheritanceHierarchy hierarchy;
		private readonly IContainerConfiguration configuration;
		private readonly Func<AssemblyName, bool> assemblyFilter;
		public IShutdownCoordinator ShutdownCoordinator { get; private set; }

		public HostingEnvironment(IInheritanceHierarchy hierarchy, IContainerConfiguration configuration,
			Func<AssemblyName, bool> assemblyFilter)
		{
			this.hierarchy = hierarchy;
			this.configuration = configuration;
			this.assemblyFilter = assemblyFilter;
			ShutdownCoordinator = new ShutdownCoordinator();
		}

		public ContainerHost CreateHost(Assembly primaryAssembly, Action<ContainerConfigurationBuilder> configure)
		{
			var targetAssemblies = Utils.Closure(primaryAssembly, ReferencedAssemblies).ToSet();
			var restrictedHierarchy = new AssembliesRestrictedInheritanceHierarchy(targetAssemblies, hierarchy);
			return new ContainerHost(restrictedHierarchy, configuration, configure, ShutdownCoordinator);
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

		//internal-конфига (FactoryConfigurator, GenericConfigurator) должна реюзатьс€, т.к.
		//ее создание очень затратно - надо заенумить вообще все типы во всех сборках.

		//естественно, реюзаетс€ дерево зависимостей

		//сами экземпл€ры конфигураторов тоже реюзаютс€

		//от одного вызова GetHost к другому мен€етс€ только приоритет одних конфигураторов перед другими
		//на основе их принадлежности PrimaryAssembly
	}
}