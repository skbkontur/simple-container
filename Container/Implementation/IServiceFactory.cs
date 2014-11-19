using System;

namespace SimpleContainer.Implementation
{
	public interface IServiceFactory
	{
		object Create(Type type, string contract, object arguments);
	}
}