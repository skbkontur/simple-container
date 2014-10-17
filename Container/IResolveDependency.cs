using System;

namespace SimpleContainer
{
	public interface IResolveDependency
	{
		object Get(Type type);
	}
}