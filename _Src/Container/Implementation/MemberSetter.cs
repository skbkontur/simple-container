using System;
using System.Reflection;

namespace SimpleContainer.Implementation
{
	public struct MemberSetter
	{
		public MemberInfo member;
		public Action<object, object> setter;
	}
}