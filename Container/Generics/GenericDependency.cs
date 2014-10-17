using System;
using System.Collections.Generic;

namespace SimpleContainer.Generics
{
	public class GenericDependency
	{
		public Type Type { get; set; }
		public GenericComponent Owner { get; set; }
		public bool UseProviderInterface { get; set; }

		public void Close(Type by, ContainerConfigurationBuilder builder, ICollection<Type> closedImplementations)
		{
			Owner.Close(TypeHelpers.GetClosingTypesSequence(Type, by), builder, closedImplementations);
		}
	}
}