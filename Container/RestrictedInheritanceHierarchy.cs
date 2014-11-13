using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SimpleContainer
{
	public class RestrictedInheritanceHierarchy : IInheritanceHierarchy
	{
		private readonly Assembly assembly;
		private readonly IInheritanceHierarchy baseHierarchy;

		public RestrictedInheritanceHierarchy(Assembly assembly, IInheritanceHierarchy baseHierarchy)
		{
			this.assembly = assembly;
			this.baseHierarchy = baseHierarchy;
		}

		public IEnumerable<Type> GetOrNull(Type type)
		{
			var result = baseHierarchy.GetOrNull(type);
			return result == null ? null : result.Where(x => x.Assembly == assembly);
		}
	}
}