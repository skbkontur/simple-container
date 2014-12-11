using System;
using System.Reflection;
using SimpleContainer.Configuration;

namespace SimpleContainer
{
	public interface IStaticContainer : IContainer
	{
		IContainer CreateLocalContainer(Assembly primaryAssembly, Action<ContainerConfigurationBuilder> configure);
	}
}