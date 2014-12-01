using System;

namespace SimpleContainer.Configuration
{
	internal class ImplentationDependencyConfiguration
	{
		public string Key { get; set; }
		public object Value { get; private set; }
		public bool ValueAssigned { get; private set; }
		public Type ImplementationType { get; set; }
		public Func<IContainer, object> Factory { get; set; }

		public void UseValue(object o)
		{
			Value = o;
			ValueAssigned = true;
		}
	}
}