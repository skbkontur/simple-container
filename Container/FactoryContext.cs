using System;

namespace SimpleContainer
{
	public class FactoryContext
	{
		public IContainer container;
		public Type target;
		public string contract;
	}
}