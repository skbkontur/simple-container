using System.Collections.Generic;

namespace SimpleContainer.Helpers
{
	public interface IObjectAccessor
	{
		bool TryGet(string name, out object value);
		IEnumerable<string> GetUnused();
	}
}