using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Interface;

namespace SimpleContainer.Configuration
{
	internal class InterfaceConfiguration
	{
		public List<Type> ImplementationTypes { get; private set; }
		public object Implementation { get; private set; }
		public bool ImplementationAssigned { get; private set; }
		public Func<FactoryContext, object> Factory { get; set; }
		public bool UseAutosearch { get; set; }

		public void AddImplementation(Type type, bool clearOld)
		{
			if (ImplementationTypes == null)
				ImplementationTypes = new List<Type>();
			if (clearOld)
				ImplementationTypes.Clear();
			if (!ImplementationTypes.Contains(type))
				ImplementationTypes.Add(type);
		}

		public void UseInstance(object instance)
		{
			Factory = null;
			Implementation = instance;
			ImplementationAssigned = true;
		}

		public InterfaceConfiguration CloneWithFilter(Func<Type, bool> filter)
		{
			return new InterfaceConfiguration
			{
				Factory = Factory,
				Implementation = Implementation,
				ImplementationAssigned = ImplementationAssigned,
				ImplementationTypes = new List<Type>(ImplementationTypes.Where(filter)),
				UseAutosearch = UseAutosearch
			};
		}
	}
}