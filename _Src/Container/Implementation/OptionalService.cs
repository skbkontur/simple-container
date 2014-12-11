using SimpleContainer.Infection;

namespace SimpleContainer.Implementation
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