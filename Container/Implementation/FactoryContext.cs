using System;
using System.Collections.Generic;

namespace SimpleContainer.Implementation
{
	public class FactoryContext
	{
		public IContainer container;
		public Type target;
		public IEnumerable<string> contracts;
	}
}