using System;

namespace SimpleContainer.Interface
{
	public interface IParametersSource
	{
		bool TryGet(string name, Type type, out object value);
	}
}