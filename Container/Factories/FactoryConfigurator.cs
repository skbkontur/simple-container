using System.Collections.Specialized;
using SimpleContainer.Configuration;

namespace SimpleContainer.Factories
{
	public class FactoryConfigurator : IHandleProfile<BasicProfile>
	{
		public void Handle(NameValueCollection applicationSettings, ContainerConfigurationBuilder configurator)
		{
			var processor = new FactoryConfigurationProcessor();
			configurator.ScanTypesWith(processor.FirstRun);
		}
	}
}