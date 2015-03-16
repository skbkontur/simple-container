using System.Collections.Generic;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class ConstructionLogContext
	{
		public ISimpleLogWriter Writer { get; private set; }
		public ContainerService UsedFromService { get; set; }
		public ServiceDependency UsedFromDependency { get; set; }
		public int Indent { get; set; }
		public ISet<CacheKey> Seen { get; private set; }

		public ConstructionLogContext(ISimpleLogWriter writer)
		{
			Writer = writer;
			Seen = new HashSet<CacheKey>();
		}

		public void WriteIndent()
		{
			Writer.WriteIndent(Indent);
		}
	}
}