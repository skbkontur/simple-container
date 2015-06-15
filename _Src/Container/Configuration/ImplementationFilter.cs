using System;

namespace SimpleContainer.Configuration
{
	internal class ImplementationFilter
	{
		public string Name { get; private set; }
		public Func<Type, Type, bool> Filter { get; private set; }

		public ImplementationFilter(string name, Func<Type, Type, bool> filter)
		{
			Name = name;
			Filter = filter;
		}
	}
}