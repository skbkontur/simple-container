using System.Collections.Generic;
using System.Reflection;

namespace System
{
	public static class TypeExtensions
	{

#if NETCORE1

		public static Assembly Assembly(this Type t)
		{
			return t.GetTypeInfo().Assembly;
		}

		public static Type BaseType(this Type t)
		{
			return t.GetTypeInfo().BaseType;
		}

		public static bool ContainsGenericParameters(this Type t)
		{
			return t.GetTypeInfo().ContainsGenericParameters;
		}

		public static bool IsAbstract(this Type t)
		{
			return t.GetTypeInfo().IsAbstract;
		}

		public static bool IsAssignableFrom(this Type t, Type fromT)
		{
			return t.GetTypeInfo().IsAssignableFrom(fromT);
		}

		public static bool IsDefined(this Type t, Type defType, bool inherit)
		{
			return t.GetTypeInfo().IsDefined(defType, inherit);
		}

		public static bool IsGenericType(this Type t)
		{
			return t.GetTypeInfo().IsGenericType;
		}

		public static bool IsInterface(this Type t)
		{
			return t.GetTypeInfo().IsInterface;
		}

		public static bool IsEnum(this Type t)
		{
			return t.GetTypeInfo().IsEnum;
		}

		public static bool IsGenericTypeDefinition(this Type t)
		{
			return t.GetTypeInfo().IsGenericTypeDefinition;
		}

		public static GenericParameterAttributes GenericParameterAttributes(this Type t)
		{
			return t.GetTypeInfo().GenericParameterAttributes;
		}

		public static bool IsNestedPublic(this Type t)
		{
			return t.GetTypeInfo().IsNestedPublic;
		}

		public static bool IsNestedPrivate(this Type t)
		{
			return t.GetTypeInfo().IsNestedPrivate;
		}

		public static bool IsValueType(this Type t)
		{
			return t.GetTypeInfo().IsValueType;
		}

		public static bool IsInstanceOfType(this Type t, object fromT)
		{
			return t.GetTypeInfo().IsInstanceOfType(fromT);
		}

		public static ConstructorInfo[] GetConstructors(this Type t)
		{
			return t.GetTypeInfo().GetConstructors();
		}

		public static ConstructorInfo[] GetConstructors(this Type t, BindingFlags flags)
		{
			return t.GetTypeInfo().GetConstructors(flags);
		}

		public static ConstructorInfo GetConstructor(this Type t, Type[] parametersTypes)
		{
			return t.GetTypeInfo().GetConstructor(parametersTypes);
		}

		public static MethodInfo[] GetMethods(this Type t)
		{
			return t.GetTypeInfo().GetMethods();
		}

		public static MethodInfo[] GetMethods(this Type t, BindingFlags flags)
		{
			return t.GetTypeInfo().GetMethods(flags);
		}

		public static MethodInfo GetMethod(this Type t, string methodName)
		{
			return t.GetTypeInfo().GetMethod(methodName);
		}

		public static MethodInfo GetMethod(this Type t, string methodName, BindingFlags flags)
		{
			return t.GetTypeInfo().GetMethod(methodName, flags);
		}

		public static MethodInfo GetMethod(this Type t, string methodName, Type[] parametersTypes)
		{
			return t.GetTypeInfo().GetMethod(methodName, parametersTypes);
		}

		public static Type[] GetInterfaces(this Type t)
		{
			return t.GetTypeInfo().GetInterfaces();
		}

		public static PropertyInfo[] GetProperties(this Type t)
		{
			return t.GetTypeInfo().GetProperties();
		}

		public static PropertyInfo[] GetProperties(this Type t, BindingFlags flags)
		{
			return t.GetTypeInfo().GetProperties(flags);
		}

		public static PropertyInfo GetProperty(this Type t, string propertyName)
		{
			return t.GetTypeInfo().GetProperty(propertyName);
		}

		public static FieldInfo[] GetFields(this Type t)
		{
			return t.GetTypeInfo().GetFields();
		}

		public static FieldInfo[] GetFields(this Type t, BindingFlags flags)
		{
			return t.GetTypeInfo().GetFields(flags);
		}

		public static IEnumerable<Attribute> GetCustomAttributes(this Type t, bool inherit)
		{
			return t.GetTypeInfo().GetCustomAttributes(inherit);
		}

		public static IEnumerable<Attribute> GetCustomAttributes(this Type t, Type attributeType, bool inherit)
		{
			return t.GetTypeInfo().GetCustomAttributes(attributeType, inherit);
		}

		public static Type[] GetGenericArguments(this Type t)
		{
			return t.GetTypeInfo().GetGenericArguments();
		}

		public static Type[] GetGenericParameterConstraints(this Type t)
		{
			return t.GetTypeInfo().GetGenericParameterConstraints();
		}

		public static Type GetGenericTypeDefinition(this Type t)
		{
			return t.GetTypeInfo().GetGenericTypeDefinition();
		}

		public static Type GetNestedType(this Type t, string name, BindingFlags flags)
		{
			return t.GetTypeInfo().GetNestedType(name, flags);
		}

#else

		public static Assembly Assembly(this Type t)
		{
			return t.Assembly;
		}

		public static Type BaseType(this Type t)
		{
			return t.BaseType;
		}

		public static bool ContainsGenericParameters(this Type t)
		{
			return t.ContainsGenericParameters;
		}

		// public static bool IsArray(this Type t)
		// {
		//     return t.IsArray;
		// }

		public static bool IsAbstract(this Type t)
		{
			return t.IsAbstract;
		}

		public static bool IsGenericType(this Type t)
		{
			return t.IsGenericType;
		}

		public static bool IsInterface(this Type t)
		{
			return t.IsInterface;
		}

		public static bool IsEnum(this Type t)
		{
			return t.IsEnum;
		}

		public static bool IsGenericTypeDefinition(this Type t)
		{
			return t.IsGenericTypeDefinition;
		}

		public static bool IsNestedPublic(this Type t)
		{
			return t.IsNestedPublic;
		}

		public static bool IsNestedPrivate(this Type t)
		{
			return t.IsNestedPrivate;
		}

		public static bool IsValueType(this Type t)
		{
			return t.IsValueType;
		}


#endif
	}
}
