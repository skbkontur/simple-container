using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using SimpleContainer.Helpers.ReflectionEmit;

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
				return IsStatic(property.GetGetMethod()) || IsStatic(property.GetSetMethod());
			var field = memberInfo as FieldInfo;
			if (field != null)
				return field.IsStatic;
			var method = memberInfo as MethodBase;
			return method != null && method.IsStatic;
		}

		public static IEnumerable<TAttribute> GetCustomAttributes<TAttribute>(this ICustomAttributeProvider attributeProvider,
			bool inherit = true)
		{
			return attributeProvider.GetCustomAttributesCached(typeof (TAttribute), inherit).Cast<TAttribute>();
		}

		public static IEnumerable<Attribute> GetCustomAttributesCached(this ICustomAttributeProvider attributeProvider,
			Type type, bool inherit = true)
		{
			return (IEnumerable<Attribute>) AttributesCache.instance.GetCustomAttributes(attributeProvider, type, inherit);
		}

		public static TAttribute GetCustomAttributeOrNull<TAttribute>(this ICustomAttributeProvider type, bool inherit = true)
		{
			return type.GetCustomAttributes<TAttribute>(inherit).SingleOrDefault();
		}

		public static bool TryGetCustomAttribute<TAttribute>(this ICustomAttributeProvider memberInfo, out TAttribute result)
		{
			return memberInfo.GetCustomAttributes<TAttribute>().TrySingle(out result);
		}

		public static bool IsDefined<TAttribute>(this ICustomAttributeProvider type, bool inherit = true)
			where TAttribute : Attribute
		{
			return type.GetCustomAttributes<TAttribute>(inherit).Any();
		}

		public static bool IsDefined(this ICustomAttributeProvider customAttributeProvider, string attributeName)
		{
			return customAttributeProvider.GetCustomAttributes(true).Any(a => a.GetType().Name == attributeName);
		}

		public static bool IsNullableOf(this Type type1, Type type2)
		{
			return Nullable.GetUnderlyingType(type1) == type2;
		}

		public static IEnumerable<Type> Parents(this Type type)
		{
			var current = type;
			while (current.BaseType != null)
			{
				yield return current.BaseType;
				current = current.BaseType;
			}
		}

		public static IEnumerable<Type> ParentsOrSelf(this Type type)
		{
			return type.Parents().Prepend(type);
		}

		public static Func<object, object[], object> EmitCallOf(MethodBase targetMethod)
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
				if (targetMethod.DeclaringType.IsValueType)
				{
					il.Emit(OpCodes.Unbox_Any, targetMethod.DeclaringType);
					il.DeclareLocal(targetMethod.DeclaringType);
					il.Emit(OpCodes.Stloc_0);
					il.Emit(OpCodes.Ldloca_S, 0);
				}
				else
					il.Emit(OpCodes.Castclass, targetMethod.DeclaringType);
			}
			var parameters = targetMethod.GetParameters();
			for (var i = 0; i < parameters.Length; i++)
			{
				il.Emit(OpCodes.Ldarg_1);
				if (i <= 8)
					il.Emit(ToConstant(i));
				else
					il.Emit(OpCodes.Ldc_I4, i);
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

		private static OpCode ToConstant(int i)
		{
			switch (i)
			{
				case 0:
					return OpCodes.Ldc_I4_0;
				case 1:
					return OpCodes.Ldc_I4_1;
				case 2:
					return OpCodes.Ldc_I4_2;
				case 3:
					return OpCodes.Ldc_I4_3;
				case 4:
					return OpCodes.Ldc_I4_4;
				case 5:
					return OpCodes.Ldc_I4_5;
				case 6:
					return OpCodes.Ldc_I4_6;
				case 7:
					return OpCodes.Ldc_I4_7;
				case 8:
					return OpCodes.Ldc_I4_8;
				default:
					throw new InvalidOperationException("method can't have more than 9 parameters");
			}
		}

		public static string FormatName(this Type type)
		{
			var result = type.Name;
			if (type.IsArray)
				return type.GetElementType().FormatName() + "[]";
			if (type.IsGenericType)
			{
				result = result.Substring(0, result.IndexOf("`", StringComparison.InvariantCulture));
				result += "<" + type.GetGenericArguments().Select(FormatName).JoinStrings(",") + ">";
			}
			return result;
		}

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
	}
}