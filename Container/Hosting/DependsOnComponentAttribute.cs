using System;

namespace SimpleContainer.Hosting
{
	public class DependsOnComponentAttribute : Attribute
	{
		public DependsOnComponentAttribute(Type serviceType)
		{
			ServiceType = serviceType;
		}

		public Type ServiceType { get; private set; }
	}
}