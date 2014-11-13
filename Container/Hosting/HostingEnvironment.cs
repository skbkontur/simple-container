using System;
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

		public HostingEnvironment(IInheritanceHierarchy hierarchy, IContainerConfiguration configuration,
			Func<AssemblyName, bool> assemblyFilter)
		{
			this.hierarchy = hierarchy;
			this.configuration = configuration;
			this.assemblyFilter = assemblyFilter;
		}

		public SimpleContainer CreateContainer(Action<ContainerConfigurationBuilder> configure)
		{
			var configurationBuilder = new ContainerConfigurationBuilder();
			configure(configurationBuilder);
			return new SimpleContainer(new MergedConfiguration(configuration, configurationBuilder.Build()), hierarchy);
		}

		public ContainerHost CreateHost(Assembly primaryAssembly)
		{
			var targetAssemblies = Utils.Closure(primaryAssembly,
				a => a.GetReferencedAssemblies()
					.Concat(a.GetCustomAttributes<ContainerReferenceAttribute>().Select(x => new AssemblyName(x.AssemblyName)))
					.Where(assemblyFilter).Select(Assembly.Load))
				.ToSet();
			var restrictedHierarchy = new AssembliesRestrictedInheritanceHierarchy(targetAssemblies, hierarchy);
			return new ContainerHost(restrictedHierarchy, configuration);
		}

		//internal-конфига (FactoryConfigurator, GenericConfigurator) должна реюзатьс€, т.к.
		//ее создание очень затратно - надо заенумить вообще все типы во всех сборках.

		//естественно, реюзаетс€ дерево зависимостей

		//сами экземпл€ры конфигураторов тоже реюзаютс€

		//от одного вызова GetHost к другому мен€етс€ только приоритет одних конфигураторов перед другими
		//на основе их принадлежности PrimaryAssembly
	}
}