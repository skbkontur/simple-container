using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using SimpleContainer.Helpers.ReflectionEmit;

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
			else if (t.IsGenericType)
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

		public static Type UnwrapEnumerable(this Type type)
		{
			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof (IEnumerable<>))
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

		public static TAttribute[] GetCustomAttributes<TAttribute>(this ICustomAttributeProvider attributeProvider,
			bool inherit = true)
		{
			return (TAttribute[]) (object) attributeProvider.GetCustomAttributesCached(typeof (TAttribute), inherit);
		}

		public static IEnumerable<Attribute> GetCustomAttributesCached(this ICustomAttributeProvider attributeProvider,
			Type type, bool inherit = true)
		{
			return (IEnumerable<Attribute>) AttributesCache.instance.GetCustomAttributes(attributeProvider, type, inherit);
		}

		public static bool TryGetCustomAttribute<TAttribute>(this ICustomAttributeProvider memberInfo, out TAttribute result)
			where TAttribute : Attribute
		{
			var attributes = memberInfo.GetCustomAttributes<TAttribute>();
			if (attributes.Length == 1)
			{
				result = attributes[0];
				return true;
			}
			result = null;
			return false;
		}

		public static bool IsDefined<TAttribute>(this ICustomAttributeProvider type, bool inherit = true)
			where TAttribute : Attribute
		{
			return type.GetCustomAttributes<TAttribute>(inherit).Any();
		}

		public static bool IsDefined(this ICustomAttributeProvider customAttributeProvider, string attributeName)
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
			if (interfaceDefinition.IsInterface)
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
					current = current.BaseType;
				}
			}
			return result;
		}

		private static readonly ConcurrentDictionary<MethodBase, Func<object, object[], object>> compiledMethods =
			new ConcurrentDictionary<MethodBase, Func<object, object[], object>>();

		private static readonly Func<MethodBase, Func<object, object[], object>> compileMethodDelegate =
			EmitCallOf;

		public static Func<object, object[], object> Compile(this MethodBase method)
		{
			return compiledMethods.GetOrAdd(method, compileMethodDelegate);
		}

		private static Func<object, object[], object> EmitCallOf(MethodBase targetMethod)
		{
			var dynamicMethod = new DynamicMethod("",
				typeof (object),
				new[] {typeof (object), typeof (object[])},
				typeof (ReflectionHelpers),
				true);
			var il = dynamicMethod.GetILGenerator();
			if (!targetMethod.IsStatic && !targetMethod.IsConstructor)
			{
				il.Emit(OpCodes.Ldarg_0);
				var declaringType = targetMethod.DeclaringType;
				if (declaringType == null)
					throw new InvalidOperationException(string.Format("DeclaringType is null for [{0}]", targetMethod));
				if (declaringType.IsValueType)
				{
					il.Emit(OpCodes.Unbox_Any, declaringType);
					il.DeclareLocal(declaringType);
					il.Emit(OpCodes.Stloc_0);
					il.Emit(OpCodes.Ldloca_S, 0);
				}
				else
					il.Emit(OpCodes.Castclass, declaringType);
			}
			var parameters = targetMethod.GetParameters();
			for (var i = 0; i < parameters.Length; i++)
			{
				il.Emit(OpCodes.Ldarg_1);
				il.EmitLdInt32(i);
				il.Emit(OpCodes.Ldelem_Ref);
				var unboxingCaster = new UnboxingCaster(typeof (object), parameters[i].ParameterType);
				unboxingCaster.EmitCast(il);
			}
			Type returnType;
			if (targetMethod.IsConstructor)
			{
				var constructorInfo = (ConstructorInfo) targetMethod;
				returnType = constructorInfo.DeclaringType;
				il.Emit(OpCodes.Newobj, constructorInfo);
			}
			else
			{
				var methodInfo = (MethodInfo) targetMethod;
				returnType = methodInfo.ReturnType;
				il.Emit(dynamicMethod.IsStatic ? OpCodes.Call : OpCodes.Callvirt, methodInfo);
			}
			if (returnType == typeof (void))
				il.Emit(OpCodes.Ldnull);
			else
			{
				var resultCaster = new BoxingCaster(typeof (object), returnType);
				resultCaster.EmitCast(il);
			}
			il.Emit(OpCodes.Ret);
			return (Func<object, object[], object>) dynamicMethod.CreateDelegate(typeof (Func<object, object[], object>));
		}

		public static string FormatName(this Type type)
		{
			string result;
			if (typeNames.TryGetValue(type, out result))
				return result;
			if (type.IsArray)
				return type.GetElementType().FormatName() + "[]";
			if (type.IsDelegate() && type.IsNested)
				return type.DeclaringType.FormatName() + "." + type.Name;

			if (!type.IsNested || !type.DeclaringType.IsGenericType || type.IsGenericParameter)
				return FormatGenericType(type, type.GetGenericArguments());

			var declaringHierarchy = DeclaringHierarchy(type)
				.TakeWhile(t => t.IsGenericType)
				.Reverse();
			
			var knownGenericArguments = type.GetGenericTypeDefinition().GetGenericArguments()
				.Zip(type.GetGenericArguments(), (definition, closed) => new {definition, closed})
				.ToDictionary(x => x.definition.GenericParameterPosition, x => x.closed);
				
			var hierarchyNames = new List<string>();

			foreach (var t in declaringHierarchy)
			{
				var tArguments = t.GetGenericTypeDefinition()
					.GetGenericArguments()
					.Where(x => knownGenericArguments.ContainsKey(x.GenericParameterPosition))
					.ToArray();

				hierarchyNames.Add(FormatGenericType(t, tArguments.Select(x => knownGenericArguments[x.GenericParameterPosition]).ToArray()));

				foreach (var tArgument in tArguments)
					knownGenericArguments.Remove(tArgument.GenericParameterPosition);
			}
			return string.Join(".", hierarchyNames.ToArray());
		}

		private static IEnumerable<Type> DeclaringHierarchy(Type type)
		{
			yield return type;
			while (type.DeclaringType != null)
			{
				yield return type.DeclaringType;
				type = type.DeclaringType;
			}
		}

		private static string FormatGenericType(Type type, Type[] arguments)
		{
			var genericMarkerIndex = type.Name.IndexOf("`", StringComparison.InvariantCulture);
			return genericMarkerIndex > 0
				? string.Format("{0}<{1}>", type.Name.Substring(0, genericMarkerIndex), arguments.Select(FormatName).JoinStrings(","))
				: type.Name;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Type GetDefinition(this Type type)
		{
			return type.IsGenericType && !type.IsGenericTypeDefinition ? type.GetGenericTypeDefinition() : type;
		}

		public static bool IsSimpleType(this Type type)
		{
			if (simpleTypes.Contains(type) || type.IsEnum)
				return true;
			var nullableWrapped = Nullable.GetUnderlyingType(type);
			return nullableWrapped != null && nullableWrapped.IsSimpleType();
		}

		public static bool IsDelegate(this Type type)
		{
			return type.BaseType == typeof (MulticastDelegate);
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