using System;

namespace SimpleContainer
{
	public interface IContainerConfiguration
	{
		bool CanCreateChildContainers { get; }
		Action ResetAction { get; }
		T GetOrNull<T>(Type type) where T: class;
		IContainerConfiguration GetByKeyOrNull(string contextKey);
		string HostName { get; }
	}
}