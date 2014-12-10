using SimpleContainer.Helpers;

namespace SimpleContainer
{
	public class ServiceInstance<T>
	{
		public T Instance { get; private set; }
		public string UsedContracts { get; private set; }

		public ServiceInstance(T instance, string usedContracts)
		{
			Instance = instance;
			UsedContracts = usedContracts;
		}

		public string FormatName()
		{
			var result = Instance.GetType().FormatName();
			if (!string.IsNullOrEmpty(UsedContracts))
				result += "[" + UsedContracts + "]";
			return result;
		}

		public ServiceInstance<TResult> Cast<TResult>()
		{
			return new ServiceInstance<TResult>((TResult) (object) Instance, UsedContracts);
		}
	}
}