using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleContainer.Implementation
{
	internal class FilteredInheritanceHierarchy : IInheritanceHierarchy
	{
		private readonly IInheritanceHierarchy decorated;
		private readonly Func<Type, bool> filter;

		public FilteredInheritanceHierarchy(IInheritanceHierarchy decorated, Func<Type, bool> filter)
		{
			this.decorated = decorated;
			this.filter = filter;
		}

		public IEnumerable<Type> GetOrNull(Type type)
		{
			var result = decorated.GetOrNull(type);
			return result == null ? null : result.Where(filter);
		}
	}
}