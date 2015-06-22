using System;
using System.Collections.Generic;
using SimpleContainer.Configuration;

namespace SimpleContainer.Generics
{
	internal class GenericDependency
	{
		public Type Type { get; set; }
		public GenericComponent Owner { get; set; }

		public void Close(Type type, GenericMappingsBuilder builder, ICollection<Type> closedImplementations)
		{
			var closingTypes = Owner.Type.MatchOrNull(Type, type);
			if (closingTypes != null)
				Owner.Close(closingTypes, builder, closedImplementations);
		}
	}
}