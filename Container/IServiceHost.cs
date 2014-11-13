using System;

namespace SimpleContainer
{
	public interface IServiceHost
	{
		IDisposable StartHosting<T>(out T service);
	}
}