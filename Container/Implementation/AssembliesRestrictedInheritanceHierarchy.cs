using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SimpleContainer.Implementation
{
	internal class AssembliesRestrictedInheritanceHierarchy : IInheritanceHierarchy
	{
		private readonly ISet<Assembly> assemblies;
		private readonly IInheritanceHierarchy baseHierarchy;

		public AssembliesRestrictedInheritanceHierarchy(ISet<Assembly> assemblies, IInheritanceHierarchy baseHierarchy)
		{
			this.assemblies = assemblies;
			this.baseHierarchy = baseHierarchy;
		}

		public IEnumerable<Type> GetOrNull(Type type)
		{
			var result = baseHierarchy.GetOrNull(type);
			return result == null ? null : result.Where(x => assemblies.Contains(x.Assembly));
		}
	}
}