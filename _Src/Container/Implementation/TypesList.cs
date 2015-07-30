using System;
using System.Collections.Generic;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	public class TypesList
	{
		public Type[] Types { get; private set; }
		private readonly Dictionary<Type, List<Type>> inheritors;

		public TypesList(Type[] types, Dictionary<Type, List<Type>> inheritors)
		{
			Types = types;
			this.inheritors = inheritors;
		}

		public List<Type> InheritorsOf(Type type)
		{
			List<Type> result;
			return inheritors.TryGetValue(type, out result) ? result : InternalHelpers.emptyTypesList;
		}

		public static TypesList Create(Type[] types)
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
				while (current != null && current != typeof (object))
				{
					Include(result, current.GetDefinition(), t);
					current = current.BaseType;
				}
			}
			return new TypesList(types, result);
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