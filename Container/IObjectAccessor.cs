using System.Collections.Generic;

namespace SimpleContainer
{
	public interface IObjectAccessor
	{
		IEnumerable<KeyValuePair<string, object>> GetValues(object o);
		bool TryGet(object o, string name, out object value);
	}
}