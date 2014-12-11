using System;

namespace SimpleContainer.Infection
{
	[AttributeUsage(AttributeTargets.Parameter)]
	public class OptionalAttribute : Attribute
	{
	}
}