using System;

namespace SimpleContainer.Helpers.ReflectionEmit
{
	public interface IMemberAccessor: IAccessMember
	{
		bool CanGet { get; }
		bool CanSet { get; }
		Type MemberType { get; }
	}
}