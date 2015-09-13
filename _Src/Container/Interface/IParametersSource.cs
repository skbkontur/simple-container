using System;
using System.Collections.Generic;

namespace SimpleContainer.Interface
{
	public interface IParametersSource
	{
		IEnumerable<string> Names { get; }
		bool TryGet(string name, Type type, out object value);
	}
}