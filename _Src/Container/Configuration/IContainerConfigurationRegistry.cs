using System;

namespace SimpleContainer.Configuration
{
	internal interface IContainerConfigurationRegistry
	{
		T GetOrNull<T>(Type type) where T : class;
	}
}