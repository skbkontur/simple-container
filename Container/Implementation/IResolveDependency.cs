using System;

namespace SimpleContainer.Implementation
{
	public interface IResolveDependency
	{
		object Get(Type type);
	}
}