using System;

namespace SimpleContainer.Interface
{
#if !NETCORE1
	[System.Runtime.Serialization.Serializable]
#endif
	public class SimpleContainerException : Exception
	{
		public SimpleContainerException(string message)
			: base(message)
		{
		}

		public SimpleContainerException(string message, Exception innerException) : base(message, innerException)
		{
		}

#if !NETCORE1
		protected SimpleContainerException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
#endif
	}
}