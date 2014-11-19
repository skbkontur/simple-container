using System;

namespace SimpleContainer.Infection
{
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public class InjectAttribute : Attribute
	{
	}
}