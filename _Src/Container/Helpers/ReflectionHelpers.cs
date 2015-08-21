using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using SimpleContainer.Helpers.ReflectionEmit;
using SimpleContainer.Implementation.Hacks;

namespace SimpleContainer.Helpers
{
	internal static class ReflectionHelpers
	{
		public static HashSet<Type> GenericParameters(this Type type)
		{
			var result = new HashSet<Type>();
			FillGenericParameters(type, result);
			return result;
		}

		private static void FillGenericParameters(Type t, HashSet<Type> result)
		{
			if (t.IsGenericParameter)
				result.Add(t);
			else if (t.GetTypeInfo().IsGenericType)
				foreach (var x in t.GetGenericArguments())
					FillGenericParameters(x, result);
		}

		public static Type TryCloseByPattern(this Type definition, Type pattern, Type value)
		{
			var argumentsCount = definition.GetGenericArguments().Length;
			var arguments = new Type[argumentsCount];
			if (!pattern.TryMatchWith(value, arguments))
				return null;
			foreach (var argument in arguments)
				if (argument == null)
					return null;
			return definition.MakeGenericType(arguments);
		}

		private static bool TryMatchWith(this Type pattern, Type value, Type[] matched)
		{
			if (pattern.IsGenericParameter)
			{
				if (value.IsGenericParameter)
					return true;
				foreach (var constraint in pattern.GetTypeInfo().GetGenericParameterConstraints())
					if (!constraint.IsAssignableFrom(value))
						return false;
				if (pattern.GetTypeInfo().GenericParameterAttributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint))
					if (value.GetConstructor(InternalHelpers.emptyTypesList) == null)
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

		public static Type UnwrapEnumerable(this Type type)
		{
			if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
				return type.GetGenericArguments()[0];
			return type.IsArray ? type.GetElementType() : type;
		}

		public static Type MemberType(this MemberInfo memberInfo)
		{
			if (memberInfo is PropertyInfo) return (memberInfo as PropertyInfo).PropertyType;
			if (memberInfo is FieldInfo) return (memberInfo as FieldInfo).FieldType;
			return null;
		}

		public static bool IsStatic(this MemberInfo memberInfo)
		{
			if (memberInfo == null)
				return false;
			var property = memberInfo as PropertyInfo;
			if (property != null)
				return IsStatic(property.GetGetMethod()) || IsStatic(property.GetSetMethod());
			var field = memberInfo as FieldInfo;
			if (field != null)
				return field.IsStatic;
			var method = memberInfo as MethodBase;
			return method != null && method.IsStatic;
		}

		public static TAttribute[] GetCustomAttributes<TAttribute>(this object attributeProvider,
			bool inherit = true)
		{
			return (TAttribute[])(object)attributeProvider.GetCustomAttributesCached(typeof(TAttribute), inherit);
		}

		public static IEnumerable<Attribute> GetCustomAttributesCached(this object attributeProvider,
			Type type, bool inherit = true)
		{
			return (IEnumerable<Attribute>)AttributesCache.instance.GetCustomAttributes(attributeProvider, type, inherit);
		}

		public static bool TryGetCustomAttribute<TAttribute>(this object memberInfo, out TAttribute result)
		{
			return memberInfo.GetCustomAttributes<TAttribute>().TrySingle(out result);
		}

		public static bool IsDefined<TAttribute>(this Type type, bool inherit = true)
			where TAttribute : Attribute
		{
			return type.GetCustomAttributes<TAttribute>(inherit).Any();
		}

		public static bool IsDefined(this Type customAttributeProvider, string attributeName)
		{
			return customAttributeProvider.GetCustomAttributes(false).Any(a => a.GetType().Name == attributeName);
		}

		public static bool IsDefined(this MemberInfo customAttributeProvider, string attributeName)
		{
			return customAttributeProvider.GetCustomAttributes(false).Any(a => a.GetType().Name == attributeName);
		}

		public static bool IsDefined(this ParameterInfo customAttributeProvider, string attributeName)
		{
			return customAttributeProvider.GetCustomAttributes(false).Any(a => a.GetType().Name == attributeName);
		}

		public static bool IsNullableOf(this Type type1, Type type2)
		{
			return Nullable.GetUnderlyingType(type1) == type2;
		}

		public static List<Type> ImplementationsOf(this Type implementation, Type interfaceDefinition)
		{
			var result = new List<Type>();
			if (interfaceDefinition.GetTypeInfo().IsInterface)
			{
				var interfaces = implementation.GetInterfaces();
				foreach (var interfaceImpl in interfaces)
					if (interfaceImpl.GetDefinition() == interfaceDefinition)
						result.Add(interfaceImpl);
			}
			else
			{
				var current = implementation;
				while (current != null)
				{
					if (current.GetDefinition() == interfaceDefinition)
					{
						result.Add(current);
						break;
					}
					current = current.GetTypeInfo().BaseType;
				}
			}
			return result;
		}

		private static readonly NonConcurrentDictionary<MethodBase, Func<object, object[], object>> compiledMethods =
			new NonConcurrentDictionary<MethodBase, Func<object, object[], object>>();

		private static readonly Func<MethodBase, Func<object, object[], object>> compileMethodDelegate =
			EmitCallOf;

		public static Func<object, object[], object> Compile(this MethodBase method)
		{
			return compiledMethods.GetOrAdd(method, compileMethodDelegate);
		}

		private static Func<object, object[], object> EmitCallOf(MethodBase targetMethod)
		{
			throw new NotSupportedException();
		}

		public static string FormatName(this Type type)
		{
			string result;
			if (typeNames.TryGetValue(type, out result))
				return result;
			result = type.Name;
			if (type.IsArray)
				return type.GetElementType().FormatName() + "[]";
			if (type.GetTypeInfo().IsGenericType)
			{
				result = result.Substring(0, result.IndexOf("`", StringComparison.OrdinalIgnoreCase));
				result += "<" + type.GetGenericArguments().Select(FormatName).JoinStrings(",") + ">";
			}
			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Type GetDefinition(this Type type)
		{
			return type.GetTypeInfo().IsGenericType && !type.GetTypeInfo().IsGenericTypeDefinition ? type.GetGenericTypeDefinition() : type;
		}

		public static bool IsSimpleType(this Type type)
		{
			if (simpleTypes.Contains(type) || type.GetTypeInfo().IsEnum)
				return true;
			var nullableWrapped = Nullable.GetUnderlyingType(type);
			return nullableWrapped != null && nullableWrapped.IsSimpleType();
		}

		private static readonly ISet<Type> simpleTypes = new HashSet<Type>
		{
			typeof (byte),
			typeof (short),
			typeof (ushort),
			typeof (int),
			typeof (uint),
			typeof (long),
			typeof (ulong),
			typeof (double),
			typeof (float),
			typeof (string),
			typeof (Guid),
			typeof (bool),
			typeof (DateTime),
			typeof (TimeSpan)
		};

		private static readonly IDictionary<Type, string> typeNames = new Dictionary<Type, string>
		{
			{typeof (object), "object"},
			{typeof (byte), "byte"},
			{typeof (short), "short"},
			{typeof (ushort), "ushort"},
			{typeof (int), "int"},
			{typeof (uint), "uint"},
			{typeof (long), "long"},
			{typeof (ulong), "ulong"},
			{typeof (double), "double"},
			{typeof (float), "float"},
			{typeof (string), "string"},
			{typeof (bool), "bool"}
		};
	}
}