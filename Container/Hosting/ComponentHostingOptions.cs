using System;

namespace SimpleContainer.Hosting
{
	public class ComponentHostingOptions
	{
		public bool IsBackground { get; set; }
		public Action OnStop;
		public Action OnPrepareStop;
	}
}