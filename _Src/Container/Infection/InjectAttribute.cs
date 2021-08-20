using System;
using JetBrains.Annotations;

namespace SimpleContainer.Infection
{
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	[MeansImplicitUse(ImplicitUseKindFlags.Assign)]
	public class InjectAttribute : Attribute
	{
	}
}