using System;

namespace SimpleContainer.Hosting
{
	public interface IComponentLogger
	{
		IDisposable OnRunComponent(Type componentType);
	}
}