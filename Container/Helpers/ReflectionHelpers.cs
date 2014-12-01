using System;
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
		private static readonly IDictionary<Type, object> defaultValues =
			new Dictionary<Type, object>
			{
				{ typeof (byte), default(byte) },
				{ typeof (short), default(short) },
				{ typeof (ushort), default(ushort) },
				{ typeof (int), default(int) },
				{ typeof (uint), default(uint) },
				{ typeof (long), default(long) },
				{ typeof (ulong), default(ulong) },
				{ typeof (double), default(double) },
				{ typeof (float), default(float) },
				{ typeof (Guid), default(Guid) },
				{ typeof (bool), default(bool) },
				{ typeof (DateTime), default(DateTime) }
			};

		public static object GetDefaultValue(Type type)
		{
			if (!type.IsValueType)
				return null;
			if (Nullable.GetUnderlyingType(type) != null)
				return null;
			if (type.IsEnum)
				return Enum.ToObject(type, -1);
			object result;
			return defaultValues.TryGetValue(type, out result) ? result : Activator.CreateInstance(type);
		}

		public static Type GetDefinition(this Type type)
		{
			return type.IsGenericType && !type.IsGenericTypeDefinition ? type.GetGenericTypeDefinition() : type;
		}

		public static Type ArgumentOf(this Type type, Type definition)
		{
			if (!definition.IsGenericTypeDefinition)
				throw new InvalidOperationException(string.Format("type [{0}] is not a generic type definition", definition.FormatName()));
			if (definition.GetGenericArguments().Length != 1)
				throw new InvalidOperationException(string.Format("type [{0}] has more than one generic argument", definition.FormatName()));
			var closedDefinitions = type.GetInterfaces().Union(type.ParentsOrSelf()).Where(x => x.GetDefinition() == definition).ToArray();
			if (closedDefinitions.Length == 0)
				throw new InvalidOperationException(string.Format("type [{0}] has no implementations of [{1}]", type.FormatName(), definition.FormatName()));
			if (closedDefinitions.Length > 1)
				throw new InvalidOperationException(string.Format("type [{0}] has many implementations of [{1}]: [{2}]",
																  type.FormatName(), definition.FormatName(),
																  closedDefinitions.Select(x => x.FormatName()).JoinStrings(",")));
			return closedDefinitions[0].GetGenericArguments()[0];
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

		public static bool CanWrite(this MemberInfo memberInfo)
		{
			if (memberInfo is PropertyInfo)
				return (memberInfo as PropertyInfo).CanWrite;
			if (memberInfo is FieldInfo)
				return !(memberInfo as FieldInfo).IsInitOnly;
			throw new NotSupportedException();
		}

		public static IEnumerable<MemberInfo> Members(this Type type)
		{
			return type.GetProperties()
					   .Concat(type.GetFields().Cast<MemberInfo>());
		}

		public static IEnumerable<MemberInfo> AllInstanceMembers(this Type type)
		{
			const BindingFlags bindingFlags =
				BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
			return type.GetProperties(bindingFlags)
					   .Concat(type.GetFields(bindingFlags).Cast<MemberInfo>());
		}

		public static IEnumerable<TAttribute> GetCustomAttributes<TAttribute>(this ICustomAttributeProvider attributeProvider, bool inherit = true)
		{
			return attributeProvider.GetCustomAttributesCached(typeof (TAttribute), inherit).Cast<TAttribute>();
		}

		public static IEnumerable<Attribute> GetCustomAttributesCached(this ICustomAttributeProvider attributeProvider, Type type, bool inherit = true)
		{
			return (IEnumerable<Attribute>) AttributesCache.instance.GetCustomAttributes(attributeProvider, type, inherit);
		}

		public static bool IsDefined(this ICustomAttributeProvider attributeProvider, Type type, bool inherit = true)
		{
			return attributeProvider.GetCustomAttributesCached(type, inherit).Any();
		}

		public static TAttribute GetCustomAttribute<TAttribute>(this ICustomAttributeProvider type, bool inherit = true)
		{
			return type.GetCustomAttributes<TAttribute>(inherit).Single();
		}

		public static TAttribute GetCustomAttributeOrNull<TAttribute>(this ICustomAttributeProvider type, bool inherit = true)
		{
			return type.GetCustomAttributes<TAttribute>(inherit).SingleOrDefault();
		}

		public static MemberInfo[] MembersWithAttribute<TAttr>(this Type entityType)
			where TAttr: Attribute
		{
			return entityType.GetAllInstanceMembers().Where(x => x.IsDefined<TAttr>()).ToArray();
		}

		public static bool TryGetCustomAttribute<TAttribute>(this ICustomAttributeProvider memberInfo, out TAttribute result)
		{
			return memberInfo.GetCustomAttributes<TAttribute>().TrySingle(out result);
		}

		public static bool IsDefined<TAttribute>(this ICustomAttributeProvider type, bool inherit = true)
			where TAttribute: Attribute
		{
			return type.GetCustomAttributes<TAttribute>(inherit).Any();
		}

		public static TAttribute GetLastAttribute<TAttribute>(this Type targetType)
			where TAttribute: Attribute
		{
			TAttribute attribute;
			if (TryGetLastAttribute(targetType, out attribute))
				return attribute;
			throw new Exception(String.Format("cannot find attribute {0}", typeof (TAttribute)));
		}

		public static bool TryGetLastAttribute<TAttribute>(Type targetType, out TAttribute attribute)
			where TAttribute: Attribute
		{
			Type type;
			if (!targetType.ParentsOrSelf().Where(t => t.IsDefined(typeof (TAttribute), false)).TryFirst(out type))
			{
				attribute = default(TAttribute);
				return false;
			}

			return type
				.GetCustomAttributes<TAttribute>(false)
				.TrySingle(out attribute);
		}

		public static bool IsNullableOf(this Type type1, Type type2)
		{
			return Nullable.GetUnderlyingType(type1) == type2;
		}

		public static MethodInfo GetInterfaceMethod(this Type interfaceType, string methodName)
		{
			return interfaceType
				.GetInterfaces()
				.Prepend(interfaceType)
				.Select(t => t.GetMethod(methodName))
				.NotNull()
				.FirstOrDefault();
		}

		public static IEnumerable<MemberInfo> GetAllInstanceMembers(this Type type)
		{
			return type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance |
								  BindingFlags.FlattenHierarchy)
					   .Where(x => !x.IsInitOnly).Concat(
														 type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance |
																			BindingFlags.FlattenHierarchy)
															 .Cast<MemberInfo>());
		}

		public static bool IsAutoProperty(this PropertyInfo propertyInfo)
		{
			var getMethod = propertyInfo.GetGetMethod(true);
			var setMethod = propertyInfo.GetSetMethod(true);
			return getMethod != null &&
				   setMethod != null &&
				   getMethod.IsDefined<CompilerGeneratedAttribute>() &&
				   setMethod.IsDefined<CompilerGeneratedAttribute>();
		}

		public static MemberInfo ToDeclaring(this MemberInfo memberInfo)
		{
			return memberInfo.DeclaringType
							 .GetMember(memberInfo.Name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
							 .Single(x => x.DeclaringType == memberInfo.DeclaringType);
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
												  new[] { typeof (object), typeof (object[]) },
												  typeof (SimpleExpressionEvaluator),
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

		public static Type UnformatName(string name, Func<string, Type> resolve)
		{
			var openAngle = name.IndexOf("<", StringComparison.OrdinalIgnoreCase);
			if (openAngle < 0)
				return resolve(name);
			var closeAngle = name.LastIndexOf(">", StringComparison.OrdinalIgnoreCase);
			if (closeAngle < 0)
				throw new InvalidOperationException(string.Format("invalid type name {0}: closing '>' not found", name));
			var argumentsPart = name.Substring(openAngle + 1, closeAngle - openAngle - 1);
			var definitionPart = name.Substring(0, openAngle);
			var argumentTypes = argumentsPart.Split(",").Select(x => UnformatName(x, resolve)).ToArray();
			if (argumentTypes.Any(x => x == null))
				return null;
			var definition = resolve(definitionPart);
			if (definition == null)
				return null;
			if (!definition.IsGenericType)
				throw new InvalidOperationException(string.Format("invalid type name {0}: type {1} is not generic", name, definitionPart));
			if (definition.GetGenericArguments().Length != argumentTypes.Length)
				throw new InvalidOperationException(string.Format("invalid type name {0}: number of generic arguments {1} is not equal to number of actual parameters {2}",
																  name, definition.GetGenericArguments().Length, argumentTypes.Length));
			return definition.MakeGenericType(argumentTypes);
		}

		public static string FormatNameWithoutArguments(this Type type)
		{
			var result = type.Name;
			if (!type.IsGenericType)
				return result;
			var qIndex = result.IndexOf("`");
			return qIndex >= 0 ? result.Substring(0, qIndex) : result;
		}

		public static string FormatName(this Type type)
		{
			var result = type.Name;
			if (type.IsGenericType)
			{
				result = result.Substring(0, result.IndexOf("`"));
				result += "<" + type.GetGenericArguments().Select(FormatName).JoinStrings(",") + ">";
			}
			return result;
		}

		public static readonly ISet<Type> simpleTypes = new HashSet<Type>
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
															typeof (DateTime)
														};
	}
}