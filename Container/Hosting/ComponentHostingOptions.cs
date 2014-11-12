using System;
using SimpleContainer.Reflection;

namespace SimpleContainer.Hosting
{
	public class ComponentHostingOptions
	{
		internal IComponent Component;

		internal ComponentHostingOptions(IComponent component)
		{
			Component = component;
		}

		internal void Initialize()
		{
			Component.Initialize(this);
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
				var message = string.Format("error stopping component [{0}]", Component.GetType().FormatName());
				throw new SimpleContainerException(message, e);
			}
		}

		public bool IsBackground { get; set; }
		public Action OnStop;
	}
}