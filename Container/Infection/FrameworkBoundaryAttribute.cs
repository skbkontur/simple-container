using System;

namespace SimpleContainer.Infection
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
	public class FrameworkBoundaryAttribute : Attribute
	{
	}
}