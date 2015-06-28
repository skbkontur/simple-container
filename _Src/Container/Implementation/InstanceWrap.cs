using System;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class InstanceWrap
	{
		private bool runCalled;
		public object Instance { get; private set; }
		public bool Owned { get; set; }

		public InstanceWrap(object instance, bool owned)
		{
			Instance = instance;
			Owned = owned;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != GetType()) return false;
			return ReferenceEquals(Instance, ((InstanceWrap) obj).Instance);
		}

		public void EnsureRunCalled(ContainerService service, LogInfo infoLogger)
		{
			var componentInstance = Instance as IComponent;
			if (componentInstance == null)
				return;
			if (!runCalled)
				lock (this)
					if (!runCalled)
					{
						var name = new ServiceName(Instance.GetType(), service.UsedContracts);
						if (infoLogger != null)
							infoLogger(name, "run started");
						try
						{
							componentInstance.Run();
						}
						catch (Exception e)
						{
							throw new SimpleContainerException(string.Format("exception running {0}", name), e);
						}
						if (infoLogger != null)
							infoLogger(name, "run finished");
						runCalled = true;
					}
		}

		public override int GetHashCode()
		{
			return Instance.GetHashCode();
		}
	}
}