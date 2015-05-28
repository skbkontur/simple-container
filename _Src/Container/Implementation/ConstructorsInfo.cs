using System;
using System.Linq;
using System.Reflection;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	internal struct ConstructorsInfo
	{
		public readonly ConstructorInfo[] publicConstructors;

		public ConstructorsInfo(Type type)
		{
			publicConstructors = type.GetConstructors().Where(x => x.IsPublic).ToArray();
		}

		public bool TryGetConstructor(out ConstructorInfo constructor)
		{
			return publicConstructors.SafeTrySingle(out constructor) ||
			       publicConstructors.SafeTrySingle(c => c.IsDefined("ContainerConstructorAttribute"), out constructor);
		}
	}
}