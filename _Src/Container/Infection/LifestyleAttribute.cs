using System;

namespace SimpleContainer.Infection
{
	[AttributeUsage(AttributeTargets.Class)]
	public class LifestyleAttribute : Attribute
	{
		public Lifestyle Lifestyle { get; private set; }

		public LifestyleAttribute(Lifestyle lifestyle)
		{
			Lifestyle = lifestyle;
		}
	}
}