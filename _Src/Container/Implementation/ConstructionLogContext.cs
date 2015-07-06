using System;
using System.Collections.Generic;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class ConstructionLogContext
	{
		public Dictionary<Type, Func<object, string>> ValueFormatters { get; private set; }
		public ISimpleLogWriter Writer { get; private set; }
		public ServiceDependency UsedFromDependency { get; set; }
		public int Indent { get; set; }
		public ISet<ServiceName> Seen { get; private set; }

		public ConstructionLogContext(ISimpleLogWriter writer, Dictionary<Type, Func<object, string>> valueFormatters)
		{
			Writer = writer;
			Seen = new HashSet<ServiceName>();
			ValueFormatters = valueFormatters;
		}

		public void WriteIndent()
		{
			Writer.WriteIndent(Indent);
		}
	}
}