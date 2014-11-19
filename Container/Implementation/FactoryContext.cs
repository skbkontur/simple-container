using System;

namespace SimpleContainer.Implementation
{
	public class FactoryContext
	{
		public IContainer container;
		public Type target;
		public string contract;
	}
}