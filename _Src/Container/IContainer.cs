using System;
using System.Collections.Generic;
using SimpleContainer.Configuration;
using SimpleContainer.Interface;

namespace SimpleContainer
{
	public interface IContainer : IDisposable
	{
		IEnumerable<Type> GetDependencies(Type type);
		IEnumerable<Type> GetImplementationsOf(Type interfaceType);
		ResolvedService Resolve(Type type, IEnumerable<string> contracts);
		BuiltUpService BuildUp(object target, IEnumerable<string> contracts);
		ResolvedService Create(Type type, IEnumerable<string> contracts, object arguments);
		IContainer Clone(Action<ContainerConfigurationBuilder> configure);
	}
}