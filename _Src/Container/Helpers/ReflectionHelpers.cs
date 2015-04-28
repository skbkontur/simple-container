using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using SimpleContainer.Helpers.ReflectionEmit;
using SimpleContainer.Implementation.Hacks;

namespace SimpleContainer.Helpers
{
	internal static class ReflectionHelpers
	{
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
				return IsStatic(property.GetMethod) || IsStatic(property.SetMethod);
			var field = memberInfo as FieldInfo;
			if (field != null)
				return field.IsStatic;
			var method = memberInfo as MethodBase;
			return method != null && method.IsStatic;
		}

		public static IEnumerable<TAttribute> GetCustomAttributes<TAttribute>(this MemberInfo attributeProvider,
			bool inherit = true)
		{
			return attributeProvider.GetCustomAttributesCached(typeof (TAttribute), inherit).Cast<TAttribute>();
		}

		public static IEnumerable<Attribute> GetCustomAttributesCached(this MemberInfo attributeProvider,
			Type type, bool inherit = true)
		{
			return (IEnumerable<Attribute>) AttributesCache.instance.GetCustomAttributes(attributeProvider, type, inherit);
		}

		public static TAttribute GetCustomAttributeOrNull<TAttribute>(this MemberInfo type, bool inherit = true)
		{
			return type.GetCustomAttributes<TAttribute>(inherit).SingleOrDefault();
		}

		public static bool TryGetCustomAttribute<TAttribute>(this MemberInfo memberInfo, out TAttribute result)
		{
			return memberInfo.GetCustomAttributes<TAttribute>().TrySingle(out result);
		}

		public static bool IsDefined(this MemberInfo customAttributeProvider, string attributeName)
		{
			return customAttributeProvider.GetCustomAttributes<Attribute>(true).Any(a => a.GetType().Name == attributeName);
		}



		public static IEnumerable<TAttribute> GetCustomAttributes<TAttribute>(this ParameterInfo attributeProvider, bool inherit = true)
		{
			return attributeProvider.GetCustomAttributesCached(typeof(TAttribute), inherit).Cast<TAttribute>();
		}

		public static IEnumerable<Attribute> GetCustomAttributesCached(this ParameterInfo attributeProvider,
			Type type, bool inherit = true)
		{
			return (IEnumerable<Attribute>)AttributesCache.instance.GetCustomAttributes(attributeProvider, type, inherit);
		}

		public static TAttribute GetCustomAttributeOrNull<TAttribute>(this ParameterInfo type, bool inherit = true)
		{
			return type.GetCustomAttributes<TAttribute>(inherit).SingleOrDefault();
		}

		public static bool TryGetCustomAttribute<TAttribute>(this ParameterInfo memberInfo, out TAttribute result)
		{
			return memberInfo.GetCustomAttributes<TAttribute>().TrySingle(out result);
		}

		public static bool IsDefined(this ParameterInfo customAttributeProvider, string attributeName)
		{
			return customAttributeProvider.GetCustomAttributes<Attribute>(true).Any(a => a.GetType().Name == attributeName);
		}


		public static IEnumerable<TAttribute> GetCustomAttributes<TAttribute>(this Type attributeProvider, bool inherit = true)
		{
			return attributeProvider.GetCustomAttributesCached(typeof(TAttribute), inherit).Cast<TAttribute>();
		}

		public static IEnumerable<Attribute> GetCustomAttributesCached(this Type attributeProvider,
			Type type, bool inherit = true)
		{
			return (IEnumerable<Attribute>)AttributesCache.instance.GetCustomAttributes(attributeProvider, type, inherit);
		}

		public static TAttribute GetCustomAttributeOrNull<TAttribute>(this Type type, bool inherit = true)
		{
			return type.GetCustomAttributes<TAttribute>(inherit).SingleOrDefault();
		}

		public static bool TryGetCustomAttribute<TAttribute>(this Type memberInfo, out TAttribute result)
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
			return customAttributeProvider.GetCustomAttributes<Attribute>(true).Any(a => a.GetType().Name == attributeName);
		}


		public static bool IsNullableOf(this Type type1, Type type2)
		{
			return Nullable.GetUnderlyingType(type1) == type2;
		}

		public static IEnumerable<Type> Parents(this Type type)
		{
			var current = type.GetTypeInfo();
			while (current.BaseType != null)
			{
				yield return current.BaseType;
				current = current.BaseType.GetTypeInfo();
			}
		}

		public static IEnumerable<Type> ParentsOrSelf(this Type type)
		{
			return type.Parents().Prepend(type);
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

		public static Type GetDefinition(this Type type)
		{
			var typeInfo = type.GetTypeInfo();
			return typeInfo.IsGenericType && !typeInfo.IsGenericTypeDefinition ? type.GetGenericTypeDefinition() : type;
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