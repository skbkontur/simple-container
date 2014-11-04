using System.Threading;
using System.Threading.Tasks;

namespace SimpleContainer.Hosting
{
	public interface IShutdownCoordinator
	{
		CancellationToken Token { get; }
		Task ShutdownTask { get; }
		void RequestShutdown();
	}
}