using System;
using System.Reflection;
using SimpleContainer.Configuration;
using SimpleContainer.Interface;

namespace SimpleContainer
{
	public interface IStaticContainer : IContainer
	{
		IContainer CreateLocalContainer(string name, Assembly primaryAssembly,
			IParametersSource parameters, Action<ContainerConfigurationBuilder> configure);

		//hack, kill
		new IStaticContainer Clone(Action<ContainerConfigurationBuilder> configure);
	}
}