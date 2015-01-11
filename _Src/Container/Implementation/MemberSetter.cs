using System;
using System.Reflection;

namespace SimpleContainer.Implementation
{
	internal struct MemberSetter
	{
		public MemberInfo member;
		public Action<object, object> setter;
	}
}