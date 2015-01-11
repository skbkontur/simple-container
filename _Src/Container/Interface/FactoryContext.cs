using System;
using System.Collections.Generic;

namespace SimpleContainer.Interface
{
	public class FactoryContext
	{
		public IContainer container;
		public Type target;
		public IEnumerable<string> contracts;
	}
}