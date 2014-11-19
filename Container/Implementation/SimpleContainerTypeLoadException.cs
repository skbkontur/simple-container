using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace SimpleContainer.Implementation
{
	public class SimpleContainerTypeLoadException : Exception
	{
		public IEnumerable<Exception> ChildExceptions { get; private set; }

		public SimpleContainerTypeLoadException(ReflectionTypeLoadException typeLoadException)
			: base("can't load types", typeLoadException)
		{
			ChildExceptions = typeLoadException.LoaderExceptions;
		}

		public override string ToString()
		{
			var result = new StringBuilder(base.ToString());
			foreach (var childException in ChildExceptions)
			{
				result.AppendLine();
				result.AppendLine();
				result.Append("---> ");
				result.Append(childException);
			}
			return result.ToString();
		}
	}
}