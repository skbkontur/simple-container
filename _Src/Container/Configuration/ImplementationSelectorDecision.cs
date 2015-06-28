using System;

namespace SimpleContainer.Configuration
{
	public struct ImplementationSelectorDecision
	{
		public Action action;
		public Type target;
		public string comment;

		public enum Action
		{
			Include,
			Exclude
		}
	}
}