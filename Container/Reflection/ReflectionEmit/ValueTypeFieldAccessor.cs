using System;
using System.Reflection;
using System.Reflection.Emit;

namespace SimpleContainer.Reflection.ReflectionEmit
{
	public class ValueTypeFieldAccessor
	{
		private readonly Func<object, object, object> setMethod;
		private readonly Func<object, object> getMethod;

		private ValueTypeFieldAccessor(Func<object, object, object> setMethod, Func<object, object> getMethod)
		{
			this.setMethod = setMethod;
			this.getMethod = getMethod;
		}

		public object Get(object entity)
		{
			return getMethod(entity);
		}

		public object Set(object entity, object value)
		{
			return setMethod(entity, value);
		}

		public static ValueTypeFieldAccessor Create(FieldInfo fieldInfo)
		{
			return new ValueTypeFieldAccessor(CreateSetMethod(fieldInfo), CreateGetMethod(fieldInfo));
		}

		private static Func<object, object> CreateGetMethod(FieldInfo fieldInfo)
		{
			var dynamicMethod = new DynamicMethod("", typeof (object),
			                                      new[] {typeof (object)},
			                                      typeof (ValueTypeFieldAccessor), true);
			EmitGet(fieldInfo, dynamicMethod.GetILGenerator());
			return (Func<object, object>) dynamicMethod.CreateDelegate(typeof (Func<object, object>));
		}

		private static void EmitGet(FieldInfo fieldInfo, ILGenerator ilGenerator)
		{
			Type targetType = fieldInfo.DeclaringType;
			Type fieldType = fieldInfo.FieldType;

			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Unbox_Any, targetType);
			ilGenerator.Emit(OpCodes.Ldfld, fieldInfo);
			if (fieldType.IsValueType)
				ilGenerator.Emit(OpCodes.Box, fieldType);
			ilGenerator.Emit(OpCodes.Ret);
		}

		private static Func<object, object, object> CreateSetMethod(FieldInfo fieldInfo)
		{
			var dynamicMethod = new DynamicMethod("", typeof (object),
			                                      new[] {typeof (object), typeof (object)},
			                                      typeof (ValueTypeFieldAccessor), true);
			EmitSet(fieldInfo, dynamicMethod.GetILGenerator());
			return (Func<object, object, object>) dynamicMethod.CreateDelegate(typeof (Func<object, object, object>));
		}

		private static void EmitSet(FieldInfo fieldInfo, ILGenerator ilGenerator)
		{
			Type targetType = fieldInfo.DeclaringType;
			Type fieldType = fieldInfo.FieldType;

			ilGenerator.DeclareLocal(targetType);
			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Unbox_Any, targetType);
			ilGenerator.Emit(OpCodes.Stloc_0);
			ilGenerator.Emit(OpCodes.Ldloca, 0);
			ilGenerator.Emit(OpCodes.Ldarg_1);
			if (fieldType.IsValueType)
			    ilGenerator.Emit(OpCodes.Unbox_Any, fieldType);
			ilGenerator.Emit(OpCodes.Stfld, fieldInfo);
			ilGenerator.Emit(OpCodes.Ldloc_0);
			ilGenerator.Emit(OpCodes.Box, targetType);
			ilGenerator.Emit(OpCodes.Ret);
		}
	}
}