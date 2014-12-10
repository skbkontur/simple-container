using System;
using System.Collections.Generic;
using SimpleContainer.Implementation;

namespace SimpleContainer
{
	public interface IContainer : IDisposable
	{
		IEnumerable<Type> GetDependencies(Type type);
		IEnumerable<Type> GetImplementationsOf(Type interfaceType);
		IEnumerable<object> GetAll(Type type);
		object Get(Type type, IEnumerable<string> contracts);
		object Create(Type type, IEnumerable<string> contracts, object arguments);
		void BuildUp(object target);

		void DumpConstructionLog(Type type, IEnumerable<string> contracts, bool entireResolutionContext,
			ISimpleLogWriter writer);

		IEnumerable<ServiceInstance<object>> GetInstanceCache(Type type);
		IContainer Clone();
	}
}