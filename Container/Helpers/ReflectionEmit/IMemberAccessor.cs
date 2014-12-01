using System;

namespace SimpleContainer.Helpers.ReflectionEmit
{
	internal interface IMemberAccessor : IAccessMember
	{
		bool CanGet { get; }
		bool CanSet { get; }
		Type MemberType { get; }
	}
}