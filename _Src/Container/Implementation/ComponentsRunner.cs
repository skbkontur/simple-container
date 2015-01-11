using System;
using System.Collections.Concurrent;

namespace SimpleContainer.Implementation
{
	internal class ComponentsRunner
	{
		private readonly LogInfo infoLogger;
		private readonly ConcurrentDictionary<object, Component> components = new ConcurrentDictionary<object, Component>();
		private readonly Func<object, Component> createComponent = _ => new Component();

		public ComponentsRunner(LogInfo infoLogger)
		{
			this.infoLogger = infoLogger;
		}

		public void EnsureRunCalled(ContainerService containerService)
		{
			foreach (var instance in containerService.Instances)
			{
				var componentInstance = instance as IComponent;
				if (componentInstance == null)
					continue;
				var component = components.GetOrAdd(instance, createComponent);
				if (!component.runCalled)
					lock (component)
						if (!component.runCalled)
						{
							var targetType = componentInstance.GetType();
							var serviceInstance = new ServiceInstance(instance, containerService);
							if (infoLogger != null)
								infoLogger(targetType, string.Format("{0} run started", serviceInstance.FormatName()));
							componentInstance.Run();
							if (infoLogger != null)
								infoLogger(targetType, string.Format("{0} run finished", serviceInstance.FormatName()));
							component.runCalled = true;
						}
			}
		}

		private class Component
		{
			public volatile bool runCalled;
		}
	}
}