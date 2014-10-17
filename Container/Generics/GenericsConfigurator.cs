using System;
using System.Collections.Specialized;
using System.Reflection;
using SimpleContainer.Configuration;

namespace SimpleContainer.Generics
{
	public class GenericsConfigurator : IHandleProfile<BasicProfile>
	{
		private readonly Func<AssemblyName, bool> assemblyFilter;

		public GenericsConfigurator(Func<AssemblyName, bool> assemblyFilter)
		{
			this.assemblyFilter = assemblyFilter;
		}

		public void Handle(NameValueCollection applicationSettings, ContainerConfigurationBuilder configurator)
		{
			var processor = new GenericsConfigurationProcessor(assemblyFilter);
			configurator.ScanTypesWith((_, type) =>
			{
				if (!type.Assembly.FullName.Contains("FunctionalTests"))
					processor.FirstRun(type);
			});
			configurator.ScanTypesWith(delegate(ContainerConfigurationBuilder containerConfigurator, Type type)
			{
				if (!type.Assembly.FullName.Contains("FunctionalTests"))
					processor.SecondRun(containerConfigurator, type);
			});
		}
	}
}