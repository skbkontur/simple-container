using System;

namespace SimpleContainer.Tests.Helpers
{
	public class ActionDisposable : IDisposable
	{
		private readonly Action action;

		public ActionDisposable(Action action)
		{
			this.action = action;
		}

		public void Dispose()
		{
			action();
		}
	}
}