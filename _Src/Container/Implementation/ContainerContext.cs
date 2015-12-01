using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class ContainerContext
	{
		public Dictionary<Type, Func<object, string>> valueFormatters;
		public TypesList typesList;
		public LogInfo infoLogger;
		public GenericsAutoCloser genericsAutoCloser;

		public Type[] AllTypes()
		{
			return allTypes ??
			       (allTypes = typesList.Types.Where(x => x.Assembly != typeof (SimpleContainer).Assembly).ToArray());
		}

		private Type[] allTypes;
	}
}