using System;

namespace SimpleContainer.Hosting
{
	public interface IPollable
	{
		void Poll();
		TimeSpan PollInterval { get; }
	}
}