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

		private static IEnumerable<Type> SelectGenericParameters(Type type)
		{
			if (type.IsGenericParameter)
				yield return type;
			else if (type.IsGenericType)
				foreach (var t in type.GetGenericArguments().SelectMany(SelectGenericParameters))
					yield return t;
		}

		public static bool TryMatchWith(this Type pattern, Type value, Type[] matched)
		{
			if (pattern.IsGenericParameter)
			{
				if (value.IsGenericParameter)
					return true;
				foreach (var constraint in pattern.GetGenericParameterConstraints())
					if (!constraint.IsAssignableFrom(value))
						return false;
				if (pattern.GenericParameterAttributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint))
					if (value.GetConstructor(Type.EmptyTypes) == null)
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
			if (pattern.IsGenericType ^ value.IsGenericType)
				return false;
			if (!pattern.IsGenericType)
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

		public static Type[] MatchOrNull(this Type definition, Type type1, Type type2)
		{
			var argumentsCount = definition.GetGenericArguments().Length;
			var arguments = new Type[argumentsCount];
			Type pattern, value;
			if (type1.ContainsGenericParameters)
			{
				pattern = type1;
				value = type2;
			}
			else
			{
				pattern = type2;
				value = type1;
			}
			return pattern.TryMatchWith(value, arguments) && arguments.All(x => x != null) ? arguments : null;
		}

		public static IEnumerable<Type> CloseBy(this Type definition, Type interfaceType, Type implType)
		{
			var implInterfaceTypes = interfaceType.IsInterface
				? implType.GetInterfaces()
				: (interfaceType.IsAbstract ? implType.ParentsOrSelf() : Enumerable.Repeat(implType, 1));
			foreach (var implInterfaceType in implInterfaceTypes)
			{
				var closed = definition.CloseByPattern(implInterfaceType, interfaceType);
				if (closed != null)
					yield return closed;
			}
		}

		public static Type CloseByPattern(this Type definition, Type pattern, Type value)
		{
			var arguments = definition.MatchOrNull(pattern, value);
			return arguments == null ? null : definition.MakeGenericType(arguments);
		}
	}
}