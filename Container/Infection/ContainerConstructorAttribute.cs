using System;

namespace SimpleContainer.Infection
{
	[AttributeUsage(AttributeTargets.Constructor)]
	public class ContainerConstructorAttribute : Attribute
	{
	}
}