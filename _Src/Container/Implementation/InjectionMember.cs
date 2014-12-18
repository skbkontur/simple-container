using System;
using System.Reflection;

namespace SimpleContainer.Implementation
{
	public struct InjectionMember
	{
		public Action<object, object> setter;
		public MemberInfo member;
	}
}