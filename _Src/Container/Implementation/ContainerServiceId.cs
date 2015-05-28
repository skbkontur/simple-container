using System.Threading;

namespace SimpleContainer.Implementation
{
	internal class ContainerServiceId
	{
		private readonly object lockObject = new object();
		private ContainerService value;

		public bool AcquireInstantiateLock(out ContainerService service)
		{
			if (value != null)
			{
				service = value;
				return false;
			}
			Monitor.Enter(lockObject);
			if (value != null)
			{
				Monitor.Exit(lockObject);
				service = value;
				return false;
			}
			service = null;
			return true;
		}

		public void ReleaseInstantiateLock(ContainerService result)
		{
			value = result;
			Monitor.Exit(lockObject);
		}

		public bool TryGet(out ContainerService result)
		{
			if (value != null)
			{
				result = value;
				return true;
			}
			result = null;
			return false;
		}
	}
}