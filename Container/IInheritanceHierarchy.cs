using System;
using System.Collections.Generic;

namespace SimpleContainer
{
	public interface IInheritanceHierarchy
	{
		IEnumerable<Type> GetOrNull(Type type);
	}
}