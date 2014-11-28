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
		object Get(Type type, string contract);
		object Create(Type type, string contract, object arguments);
		void BuildUp(object target);
		void DumpConstructionLog(Type type, string contractName, bool entireResolutionContext, ISimpleLogWriter writer);
		IEnumerable<object> GetInstanceCache(Type type);
		IContainer Clone();
	}
}