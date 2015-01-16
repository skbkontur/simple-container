using System;
using System.Collections.Concurrent;
using SimpleContainer.Interface;

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

		public void EnsureRunCalled(ContainerService containerService, bool useCache)
		{
			foreach (var instance in containerService.Instances)
			{
				var componentInstance = instance as IComponent;
				if (componentInstance == null)
					continue;
				var component = useCache ? components.GetOrAdd(instance, createComponent) : new Component();
				if (!component.runCalled)
					lock (component)
						if (!component.runCalled)
						{
							var name = new ServiceName(instance.GetType(), containerService.FinalUsedContracts);
							if (infoLogger != null)
								infoLogger(name, "run started");
							try
							{
								componentInstance.Run();
							}
							catch (Exception e)
							{
								throw new SimpleContainerException(string.Format("exception running {0}", name.FormatName()), e);
							}
							if (infoLogger != null)
								infoLogger(name, "run finished");
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