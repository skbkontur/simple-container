using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	internal class DefaultInheritanceHierarchy : IInheritanceHierarchy
	{
		private readonly IDictionary<Type, List<Type>> inheritors;

		private DefaultInheritanceHierarchy(IDictionary<Type, List<Type>> inheritors)
		{
			this.inheritors = inheritors;
		}

		public IEnumerable<Type> GetOrNull(Type type)
		{
			return inheritors.GetOrDefault(type);
		}

		public static IInheritanceHierarchy Create(IEnumerable<Type> types)
		{
			var result = new Dictionary<Type, List<Type>>();
			foreach (var type in types.Select(x => x.GetDefinition()).Where(x => !x.IsNestedPrivate))
			{
				if (type.IsAbstract)
					continue;
				foreach (var parentType in type.GetInterfaces().Union(type.ParentsOrSelf()).Select(x => x.GetDefinition()))
				{
					List<Type> children;
					if (!result.TryGetValue(parentType, out children))
						result.Add(parentType, children = new List<Type>(1));
					children.Add(type);
				}
			}
			return new DefaultInheritanceHierarchy(result);
		}
	}
}