using System;
using System.Collections.Generic;

namespace SimpleContainer
{
	public interface IContainer
	{
		IEnumerable<Type> GetDependencies(Type type);
		IEnumerable<Type> GetImplementationsOf(Type interfaceType);
		IEnumerable<object> GetAll(Type type);
		object Get(Type type);
		void BuildUp(object target);
		void DumpConstructionLog(Type type, string contractName, bool entireResolutionContext, ISimpleLogWriter writer);
	}
}