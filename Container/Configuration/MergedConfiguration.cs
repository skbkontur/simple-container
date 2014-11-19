using System;

namespace SimpleContainer.Configuration
{
	public class MergedConfiguration: IContainerConfiguration
	{
		private readonly IContainerConfiguration parent;
		private readonly IContainerConfiguration child;

		public MergedConfiguration(IContainerConfiguration parent, IContainerConfiguration child)
		{
			this.parent = parent;
			this.child = child;
		}

		public T GetOrNull<T>(Type type) where T: class
		{
			return child.GetOrNull<T>(type) ?? parent.GetOrNull<T>(type);
		}

		public IContainerConfiguration GetByKeyOrNull(string contextKey)
		{
			return child.GetByKeyOrNull(contextKey) ?? parent.GetByKeyOrNull(contextKey);
		}
	}
}