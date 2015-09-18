using System;
using System.Reflection.Emit;

namespace SimpleContainer.Helpers
{
	internal static class EmitHelpers
	{
		public static void EmitLdInt32(this ILGenerator il, int value)
		{
			if (value <= 8)
				il.Emit(ToConstant(value));
			else
				il.Emit(OpCodes.Ldc_I4, value);
		}

		public static void EmitLdArg(this ILGenerator il, int index)
		{
			if (index <= 3)
				il.Emit(LdArg(index));
			else
				il.Emit(OpCodes.Ldarg, index);
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

		private static OpCode LdArg(int i)
		{
			switch (i)
			{
				case 0:
					return OpCodes.Ldarg_0;
				case 1:
					return OpCodes.Ldarg_1;
				case 2:
					return OpCodes.Ldarg_2;
				case 3:
					return OpCodes.Ldarg_3;
				default:
					throw new ArgumentException(string.Format("invalid argument index [{0}]", i));
			}
		}
	}
}