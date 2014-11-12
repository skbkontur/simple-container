using System;

namespace SimpleContainer.Infection
{
	public class FromResourceAttribute : Attribute
	{
		public string Name { get; private set; }

		public FromResourceAttribute(string name)
		{
			Name = name;
		}
	}
}