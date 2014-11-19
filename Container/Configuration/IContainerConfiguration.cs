using System;

namespace SimpleContainer.Configuration
{
	public interface IContainerConfiguration
	{
		T GetOrNull<T>(Type type) where T : class;
		IContainerConfiguration GetByKeyOrNull(string contextKey);
	}
}