using SimpleContainer.Infection;

namespace SimpleContainer.Interface
{
	public class OptionalService<T>
	{
		public T Service { get; private set; }

		public OptionalService([Optional] T service)
		{
			Service = service;
		}
	}
}