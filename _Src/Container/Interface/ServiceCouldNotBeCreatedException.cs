using System;

namespace SimpleContainer.Interface
{
	public class ServiceCouldNotBeCreatedException : Exception
	{
		public ServiceCouldNotBeCreatedException(string message = null) : base(message)
		{
		}
	}
}