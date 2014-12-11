using System.Reflection;

namespace SimpleContainer.Helpers.ReflectionEmit
{
	internal class UntypedMemberAccessor
	{
		public static IMemberAccessor Create(MemberInfo memberInfo)
		{
			return MemberAccessor<object>.Get(memberInfo);
		}
	}
}