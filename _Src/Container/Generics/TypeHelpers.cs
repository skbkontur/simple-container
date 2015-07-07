using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation.Hacks;

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
			else if (type.GetTypeInfo().IsGenericType)
				foreach (var t in type.GetGenericArguments().SelectMany(SelectGenericParameters))
					yield return t;
		}

		private static bool SatisfyConstraints(Type parameter, Type by)
		{
			if (parameter.GetTypeInfo().GetGenericParameterConstraints().Any(c => !c.IsAssignableFrom(by)))
				return false;
			var needDefaultConstructor = (parameter.GetTypeInfo().GenericParameterAttributes &
			                              GenericParameterAttributes.DefaultConstructorConstraint) != 0;
			return !needDefaultConstructor || by.GetTypeInfo().DeclaredConstructors.SingleOrDefault(x => x.GetParameters().Length == 0) != null;
		}

		public static bool CanClose(Type what, Type by)
		{
			if (what.IsGenericParameter)
				return SatisfyConstraints(what, by);

			if (what.GetTypeInfo().IsGenericType ^ by.GetTypeInfo().IsGenericType)
				return false;
			if (what.GetTypeInfo().IsGenericType)
			{
				if (what.GetGenericTypeDefinition() != by.GetGenericTypeDefinition())
					return false;
				var whatArguments = what.GetGenericArguments();
				var byArguments = by.GetGenericArguments();
				return whatArguments.Length == byArguments.Length && whatArguments.All((t, i) => CanClose(t, byArguments[i]));
			}
			return what == by;
		}

		public static bool TryMatchWith(this Type pattern, Type value, Type[] matched)
		{
			if (pattern.IsGenericParameter)
			{
				if (value.IsGenericParameter)
					return true;
				foreach (var constraint in pattern.GetTypeInfo().GetGenericParameterConstraints())
					if (!constraint.IsAssignableFrom(value))
						return false;
				if (pattern.GetTypeInfo().GenericParameterAttributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint))
					if (value.GetConstructors().FirstOrDefault(x=>x.GetParameters().Length == 0) == null)
						return false;
				if (matched != null)
				{
					var position = pattern.GenericParameterPosition;
					if (matched[position] != null && matched[position] != value)
						return false;
					matched[position] = value;
				}
				return true;
			}
			if (pattern.GetTypeInfo().IsGenericType ^ value.GetTypeInfo().IsGenericType)
				return false;
			if (!pattern.GetTypeInfo().IsGenericType)
				return pattern == value;
			if (pattern.GetGenericTypeDefinition() != value.GetGenericTypeDefinition())
				return false;
			var patternArguments = pattern.GetGenericArguments();
			var valueArguments = value.GetGenericArguments();
			for (var i = 0; i < patternArguments.Length; i++)
				if (!patternArguments[i].TryMatchWith(valueArguments[i], matched))
					return false;
			return true;
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
			if (!what.GetTypeInfo().IsGenericType)
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
			return type.GetInterfaces().Concat(type).Where(x => x.GetTypeInfo().IsGenericType).ToArray();
		}
	}
}