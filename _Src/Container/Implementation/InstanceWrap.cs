using System;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class InstanceWrap
	{
		private volatile bool initialized;
		public object Instance { get; private set; }
		public bool Owned { get; private set; }

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

		public void EnsureInitialized(ContainerService service, ContainerContext containerContext, ContainerService root)
		{
			if (!Owned)
				return;
			var componentInstance = Instance as IInitializable;
			if (componentInstance == null)
				return;
			if (!initialized)
				lock (this)
					if (!initialized)
					{
						var name = new ServiceName(Instance.GetType(), service.UsedContracts);
						if (containerContext.infoLogger != null)
							containerContext.infoLogger(name, "initialize started");
						try
						{
							componentInstance.Initialize();
						}
						catch (Exception e)
						{
							throw new SimpleContainerException(string.Format("exception initializing {0}\r\n\r\n{1}", name, root.GetConstructionLog(containerContext)), e);
						}
						if (containerContext.infoLogger != null)
							containerContext.infoLogger(name, "initialize finished");
						initialized = true;
					}
		}

		public override int GetHashCode()
		{
			return Instance.GetHashCode();
		}
	}
}