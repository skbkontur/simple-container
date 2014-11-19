using System;
using System.Collections.Generic;
using SimpleContainer.Implementation;

namespace SimpleContainer.Configuration
{
	public class InterfaceConfiguration
	{
		public List<Type> ImplementationTypes { get; private set; }
		public object Implementation { get; set; }
		public Func<FactoryContext, object> Factory { get; set; }
		public bool UseAutosearch { get; set; }
		public CacheLevel? CacheLevel { get; set; }

		public void AddImplementation(Type type, bool clearOld)
		{
			if (ImplementationTypes == null)
				ImplementationTypes = new List<Type>();
			if (clearOld)
				ImplementationTypes.Clear();
			if (!ImplementationTypes.Contains(type))
				ImplementationTypes.Add(type);
		}
	}
}