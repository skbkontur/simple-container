using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleContainer.Configuration
{
	internal class GenericMappingsBuilder
	{
		private readonly IDictionary<Type, List<Type>> genericMappings = new Dictionary<Type, List<Type>>();

		public void DefinedGenericMapping(Type from, Type to)
		{
			List<Type> mappings;
			if (!genericMappings.TryGetValue(from, out mappings))
				genericMappings.Add(from, mappings = new List<Type>());
			mappings.Add(to);
		}

		public IDictionary<Type, Type[]> Build()
		{
			return genericMappings.ToDictionary(x => x.Key, x => x.Value.ToArray());
		}
	}
}