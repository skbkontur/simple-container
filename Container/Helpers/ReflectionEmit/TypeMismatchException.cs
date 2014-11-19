using System;

namespace SimpleContainer.Helpers.ReflectionEmit
{
	public class TypeMismatchException : Exception
	{
		public TypeMismatchException(Type first, Type second)
			: base(string.Format("types [{0}] and [{1}] are not compatibe", first.FullName, second.FullName))
		{
		}
	}
}