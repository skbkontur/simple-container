using System;
using System.Collections.Generic;

namespace SimpleContainer.Implementation
{
	public interface IInheritanceHierarchy
	{
		IEnumerable<Type> GetOrNull(Type type);
	}
}