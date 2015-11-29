using System.Threading;

namespace SimpleContainer.Implementation
{
	internal class ContainerServiceId
	{
		private readonly object lockObject = new object();
		private ContainerService value;

		public AcquireResult AcquireInstantiateLock()
		{
			if (value != null)
				return new AcquireResult {acquired = false, alreadyConstructedService = value};
			Monitor.Enter(lockObject);
			if (value != null)
			{
				Monitor.Exit(lockObject);
				return new AcquireResult {acquired = false, alreadyConstructedService = value};
			}
			return new AcquireResult {acquired = true};
		}

		public struct AcquireResult
		{
			public bool acquired;
			public ContainerService alreadyConstructedService;
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