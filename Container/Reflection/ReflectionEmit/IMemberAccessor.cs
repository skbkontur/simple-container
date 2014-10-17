using System;

namespace SimpleContainer.Reflection.ReflectionEmit
{
	public interface IMemberAccessor: IAccessMember
	{
		bool CanGet { get; }
		bool CanSet { get; }
		Type MemberType { get; }
	}
}