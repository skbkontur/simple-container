using System.Collections.Generic;

namespace SimpleContainer.Helpers
{
	internal interface IObjectAccessor
	{
		bool TryGet(string name, out ValueWithType value);
		IEnumerable<string> GetUnused();
		IEnumerable<string> GetUsed();
	}
}