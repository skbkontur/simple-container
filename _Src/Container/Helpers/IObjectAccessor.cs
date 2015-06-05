using System.Collections.Generic;

namespace SimpleContainer.Helpers
{
	internal interface IObjectAccessor
	{
		bool TryGet(string name, out object value);
		IEnumerable<string> GetUsed();
		IEnumerable<string> GetUnused();
	}
}