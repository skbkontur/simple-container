using System;
using System.Collections.Generic;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	public static class InheritorsBuilder
	{
		public static Dictionary<Type, List<Type>> CreateInheritorsMap(Type[] types)
		{
			var result = new Dictionary<Type, List<Type>>();
			foreach (var type in types)
			{
				if (type.IsAbstract)
					continue;
				if (type.IsNestedPrivate)
					continue;
				var t = type.GetDefinition();
				foreach (var interfaceType in t.GetInterfaces())
					Include(result, interfaceType.GetDefinition(), t);
				var current = t;
				while (current != null)
				{
					Include(result, current.GetDefinition(), t);
					current = current.BaseType;
				}
			}
			return result;
		}

		private static void Include(Dictionary<Type, List<Type>> result, Type parentType, Type type)
		{
			List<Type> children;
			if (!result.TryGetValue(parentType, out children))
				result.Add(parentType, children = new List<Type>(1));
			children.Add(type);
		}
	}
}