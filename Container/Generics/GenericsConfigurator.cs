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

		public void Handle(NameValueCollection applicationSettings, ContainerConfigurationBuilder builder)
		{
			var processor = new GenericsConfigurationProcessor(assemblyFilter);
			builder.ScanTypesWith((_, type) =>
			{
				if (!type.Assembly.FullName.Contains("FunctionalTests"))
					processor.FirstRun(type);
			});
			builder.ScanTypesWith(delegate(ContainerConfigurationBuilder containerConfigurator, Type type)
			{
				if (!type.Assembly.FullName.Contains("FunctionalTests"))
					processor.SecondRun(containerConfigurator, type);
			});
		}
	}
}