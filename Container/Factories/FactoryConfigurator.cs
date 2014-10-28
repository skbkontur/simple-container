using System.Collections.Specialized;
using SimpleContainer.Configuration;

namespace SimpleContainer.Factories
{
	public class FactoryConfigurator : IHandleProfile<BasicProfile>
	{
		public void Handle(NameValueCollection applicationSettings, ContainerConfigurationBuilder builder)
		{
			var processor = new FactoryConfigurationProcessor();
			builder.ScanTypesWith(processor.FirstRun);
		}
	}
}