using System;
using System.Collections.Generic;

namespace SimpleContainer
{
	public class ImplentationDependencyConfiguration
	{
		public string Key { get; set; }
		public object Value { get; private set; }
		public bool ValueAssigned { get; private set; }
		public Type ImplementationType { get; set; }
		public Func<IContainer, object> Factory { get; set; }
		public List<string> Contracts { get; set; }

		public void UseValue(object o)
		{
			Value = o;
			ValueAssigned = true;
		}

		public void AddContract(string contract)
		{
			if (Contracts == null)
				Contracts = new List<string>(1);
			if (!Contracts.Contains(contract))
				Contracts.Add(contract);
		}
	}
}