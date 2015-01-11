using System;

namespace SimpleContainer.Interface
{
	[Serializable]
	public class SimpleContainerException : Exception
	{
		public SimpleContainerException(string message)
			: base(message)
		{
		}

		public SimpleContainerException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}
}