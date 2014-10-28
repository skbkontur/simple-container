using System.Collections.Specialized;

namespace SimpleContainer.Hosting
{
	public interface IRunnable
	{
		void Run(NameValueCollection arguments);
	}
}