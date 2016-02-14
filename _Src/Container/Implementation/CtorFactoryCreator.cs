using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal static class CtorFactoryCreator
	{
		public static bool TryCreate(ContainerService.Builder builder)
		{
			if (!builder.Type.IsDelegate() || builder.Type.FullName.StartsWith("System.Func`"))
				return false;
			var invokeMethod = builder.Type.GetMethod("Invoke");
			if (!builder.Type.IsNestedPublic)
			{
				builder.SetError(string.Format("can't create delegate [{0}]. must be nested public", builder.Type.FormatName()));
				return true;
			}
			if (invokeMethod.ReturnType != builder.Type.DeclaringType)
			{
				builder.SetError(string.Format("can't create delegate [{0}]. return type must match declaring", builder.Type.FormatName()));
				return true;
			}
			const BindingFlags ctorBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			var constructors = builder.Type.DeclaringType.GetConstructors(ctorBindingFlags)
				.Where(x => Match(invokeMethod, x))
				.ToArray();
			if (constructors.Length == 0)
			{
				builder.SetError("can't find matching ctor");
				return true;
			}
			if (constructors.Length > 1)
			{
				builder.SetError("more than one matched ctors found");
				return true;
			}
			var delegateParameters = invokeMethod.GetParameters();
			var delegateParameterNameToIndexMap = new Dictionary<string, int>();
			for (var i = 0; i < delegateParameters.Length; i++)
				delegateParameterNameToIndexMap[delegateParameters[i].Name] = i;

			var dynamicMethodParameterTypes = new Type[delegateParameters.Length + 1];
			dynamicMethodParameterTypes[0] = typeof (object[]);
			for (var i = 1; i < dynamicMethodParameterTypes.Length; i++)
				dynamicMethodParameterTypes[i] = delegateParameters[i - 1].ParameterType;

			var dynamicMethod = new DynamicMethod("", invokeMethod.ReturnType,
				dynamicMethodParameterTypes, typeof (ReflectionHelpers), true);

			var il = dynamicMethod.GetILGenerator();
			var ctorParameters = constructors[0].GetParameters();
			var serviceTypeToIndex = new Dictionary<Type, int>();
			var services = new List<object>();
			foreach (var p in ctorParameters)
			{
				int delegateParameterIndex;
				if (delegateParameterNameToIndexMap.TryGetValue(p.Name, out delegateParameterIndex))
				{
					var delegateParameterType = delegateParameters[delegateParameterIndex].ParameterType;
					if (!p.ParameterType.IsAssignableFrom(delegateParameterType))
					{
						const string messageFormat = "type mismatch for [{0}], delegate type [{1}], ctor type [{2}]";
						builder.SetError(string.Format(messageFormat,
							p.Name, delegateParameterType.FormatName(), p.ParameterType.FormatName()));
						return true;
					}
					il.EmitLdArg(delegateParameterIndex + 1);
					delegateParameterNameToIndexMap.Remove(p.Name);
				}
				else
				{
					int serviceIndex;
					if (!serviceTypeToIndex.TryGetValue(p.ParameterType, out serviceIndex))
					{
						object value;
						if (p.ParameterType == typeof (ServiceName))
							value = null;
						else
						{
							var dependency = builder.Context.Container.InstantiateDependency(p, builder).CastTo(p.ParameterType);
							builder.AddDependency(dependency, false);
							if (dependency.ContainerService != null)
								builder.UnionUsedContracts(dependency.ContainerService);
							if (builder.Status != ServiceStatus.Ok)
								return true;
							value = dependency.Value;
						}
						serviceIndex = serviceTypeToIndex.Count;
						serviceTypeToIndex.Add(p.ParameterType, serviceIndex);
						services.Add(value);
					}
					il.Emit(OpCodes.Ldarg_0);
					il.EmitLdInt32(serviceIndex);
					il.Emit(OpCodes.Ldelem_Ref);
					il.Emit(p.ParameterType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, p.ParameterType);
				}
			}
			if (delegateParameterNameToIndexMap.Count > 0)
			{
				builder.SetError(string.Format("delegate has not used parameters [{0}]",
					delegateParameterNameToIndexMap.Keys.JoinStrings(",")));
				return true;
			}
			builder.EndResolveDependencies();
			int serviceNameIndex;
			if (serviceTypeToIndex.TryGetValue(typeof (ServiceName), out serviceNameIndex))
				services[serviceNameIndex] = new ServiceName(builder.Type.DeclaringType, builder.FinalUsedContracts);
			il.Emit(OpCodes.Newobj, constructors[0]);
			il.Emit(OpCodes.Ret);
			var context = serviceTypeToIndex.Count == 0 ? null : services.ToArray();
			builder.AddInstance(dynamicMethod.CreateDelegate(builder.Type, context), true, false);
			return true;
		}

		private static bool Match(MethodInfo method, ConstructorInfo ctor)
		{
			var methodParameters = new Dictionary<string, Type>();
			foreach (var p in method.GetParameters())
				methodParameters[p.Name] = p.ParameterType;
			foreach (var p in ctor.GetParameters())
			{
				Type methodParameterType;
				if (methodParameters.TryGetValue(p.Name, out methodParameterType))
				{
					if (!p.ParameterType.IsAssignableFrom(methodParameterType))
						return false;
				}
				else if (p.ParameterType.IsSimpleType())
					return false;
			}
			return true;
		}
	}
}