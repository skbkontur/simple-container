using System;

namespace SimpleContainer.Infection
{
	[AttributeUsage(AttributeTargets.Class)]
	public class DontUseAttribute : Attribute
	{
	}
}