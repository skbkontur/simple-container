using System;

namespace SimpleContainer.Infection
{
	public class HostingAttribute: Attribute
	{
		public string[] Names { get; private set; }

		public HostingAttribute(params string[] names)
		{
			Names = names;
		}
	}
}