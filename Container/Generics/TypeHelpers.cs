using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Helpers;

namespace SimpleContainer.Generics
{
	internal static class TypeHelpers
	{
		public static bool HasEquivalentParameters(Type dependency, Type definition)
		{
			return SelectGenericParameters(dependency).Distinct().IsEquivalentTo(definition.GetGenericArguments());
		}

		public static IEnumerable<Type> SelectGenericParameters(Type type)
		{
			if (type.IsGenericParameter)
				yield return type;
			else if (type.IsGenericType)
				foreach (var t in type.GetGenericArguments().SelectMany(SelectGenericParameters))
					yield return t;
		}

		private static bool SatisfyConstraints(Type parameter, Type by)
		{
			if (parameter.GetGenericParameterConstraints().Any(c => !c.IsAssignableFrom(by)))
				return false;
			var needDefaultConstructor = (parameter.GenericParameterAttributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0;
			return !needDefaultConstructor || by.GetConstructor(Type.EmptyTypes) != null;
		}

		public static bool CanClose(Type what, Type by)
		{
			if (what.IsGenericParameter)
				return SatisfyConstraints(what, by);

			if (what.IsGenericType ^ by.IsGenericType)
				return false;
			if (what.IsGenericType)
			{
				if (what.GetGenericTypeDefinition() != by.GetGenericTypeDefinition())
					return false;
				var whatArguments = what.GetGenericArguments();
				var byArguments = by.GetGenericArguments();
				return whatArguments.Length == byArguments.Length && whatArguments.All((t, i) => CanClose(t, byArguments[i]));
			}
			return what == by;
		}

		public static Type[] GetClosingTypesSequence(Type what, Type by)
		{
			return GetClosingTypesSequenceInternal(what, by)
				.OrderBy(x => x.Key)
				.Select(x => x.Value)
				.ToArray();
		}

		private static IEnumerable<KeyValuePair<int, Type>> GetClosingTypesSequenceInternal(Type what, Type by)
		{
			if (what.IsGenericParameter)
				yield return new KeyValuePair<int, Type>(what.GenericParameterPosition, by);
			if (!what.IsGenericType)
				yield break;
			var whatArguments = what.GetGenericArguments();
			var byArguments = by.GetGenericArguments();
			for (var i = 0; i < whatArguments.Length; i ++)
				foreach (var item in GetClosingTypesSequenceInternal(whatArguments[i], byArguments[i]))
					yield return item;
		}

		public static IEnumerable<Type> FindAllClosing(Type what, IEnumerable<Type> by)
		{
			return by.Where(x => CanClose(what, x));
		}

		public static Type[] GetGenericInterfaces(Type type)
		{
			return type.GetInterfaces().Concat(type).Where(x => x.IsGenericType).ToArray();
		}
	}
}