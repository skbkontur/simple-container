using System;
using System.Reflection.Emit;

namespace SimpleContainer.Reflection.ReflectionEmit
{
	public class BoxingCaster : Caster
	{
		public BoxingCaster(Type outputType, Type memberType)
			: base(outputType, memberType) {}

		protected override void EmitNullableCast(ILGenerator ilGenerator, Type nullableType)
		{
			ilGenerator.Emit(OpCodes.Newobj, nullableType.GetConstructor(new[] { memberType }));
		}

		protected override void EmitValueTypeCast(ILGenerator ilGenerator)
		{
			ilGenerator.Emit(OpCodes.Box, memberType);
		}
	}
}