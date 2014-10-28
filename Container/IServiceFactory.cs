using System;

namespace SimpleContainer
{
	public interface IServiceFactory
	{
		object Create(Type type, string contract, object arguments);
	}
}