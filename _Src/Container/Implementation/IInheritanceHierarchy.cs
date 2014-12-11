using System;
using System.Collections.Generic;

namespace SimpleContainer.Implementation
{
	internal interface IInheritanceHierarchy
	{
		IEnumerable<Type> GetOrNull(Type type);
	}
}