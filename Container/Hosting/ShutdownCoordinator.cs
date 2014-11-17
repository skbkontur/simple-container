using System.Threading;
using System.Threading.Tasks;

namespace SimpleContainer.Hosting
{
	public class ShutdownCoordinator : IShutdownCoordinator
	{
		private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
		private readonly TaskCompletionSource<object> shutdownCompletionSource = new TaskCompletionSource<object>();

		public ShutdownCoordinator()
		{
			Token = cancellationTokenSource.Token;
			ShutdownTask = shutdownCompletionSource.Task;
		}

		public CancellationToken Token { get; private set; }
		public Task ShutdownTask { get; private set; }

		public void RequestShutdown()
		{
			cancellationTokenSource.Cancel();
			shutdownCompletionSource.SetResult(null);
		}
	}
}