using System;

namespace SimpleContainer
{
	public class MergedConfiguration: IContainerConfiguration
	{
		private readonly IContainerConfiguration parent;
		private readonly IContainerConfiguration child;

		public MergedConfiguration(IContainerConfiguration parent, IContainerConfiguration child)
		{
			this.parent = parent;
			this.child = child;
			ResetAction = delegate
						  {
							  parent.ResetAction();
							  child.ResetAction();
						  };
			HostName = parent.HostName;
		}

		public bool CanCreateChildContainers
		{
			get { return parent.CanCreateChildContainers; }
		}

		public Action ResetAction { get; private set; }

		public T GetOrNull<T>(Type type) where T: class
		{
			return child.GetOrNull<T>(type) ?? parent.GetOrNull<T>(type);
		}

		public IContainerConfiguration GetByKeyOrNull(string contextKey)
		{
			return child.GetByKeyOrNull(contextKey) ?? parent.GetByKeyOrNull(contextKey);
		}

		public string HostName { get; private set; }
	}
}