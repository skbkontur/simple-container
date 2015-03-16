using System;

namespace SimpleContainer.Interface
{
	public class ServiceCouldNotBeCreatedException : Exception
	{
		public ServiceCouldNotBeCreatedException(string message) : base(message)
		{
		}
	}
}