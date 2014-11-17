using System;
using SimpleContainer.Reflection;

namespace SimpleContainer.Hosting
{
	public class ComponentHostingOptions
	{
		internal IComponent component;

		internal ComponentHostingOptions(IComponent component)
		{
			this.component = component;
		}

		internal void Initialize()
		{
			component.Run(this);
		}

		internal void Stop()
		{
			if (OnStop == null)
				return;
			try
			{
				OnStop();
			}
			catch (Exception e)
			{
				var message = string.Format("error stopping component [{0}]", component.GetType().FormatName());
				throw new SimpleContainerException(message, e);
			}
		}

		public bool IsBackground { get; set; }
		public Action OnStop;
	}
}