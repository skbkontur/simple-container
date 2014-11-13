using System;
using System.Reflection;

namespace SimpleContainer.Hosting
{
	public class HostingEnvironment
	{
		private readonly IInheritanceHierarchy inheritors;
		private readonly IContainerConfiguration configuration;

		public HostingEnvironment(IInheritanceHierarchy inheritors, IContainerConfiguration configuration)
		{
			this.inheritors = inheritors;
			this.configuration = configuration;
		}

		public SimpleContainer CreateContainer(Action<ContainerConfigurationBuilder> configure)
		{
			var configurationBuilder = new ContainerConfigurationBuilder();
			configure(configurationBuilder);
			return new SimpleContainer(new MergedConfiguration(configuration, configurationBuilder.Build()), inheritors);
		}

		public ContainerHost CreateHost(Assembly primaryAssembly)
		{
			return new ContainerHost(inheritors, configuration, primaryAssembly);
		}

		//internal-конфига (FactoryConfigurator, GenericConfigurator) должна реюзатьс€, т.к.
		//ее создание очень затратно - надо заенумить вообще все типы во всех сборках.

		//естественно, реюзаетс€ дерево зависимостей

		//сами экземпл€ры конфигураторов тоже реюзаютс€

		//от одного вызова GetHost к другому мен€етс€ только приоритет одних конфигураторов перед другими
		//на основе их принадлежности PrimaryAssembly
	}
}