using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class NamedInstance
	{
		public object Instance { get; private set; }
		public ServiceName Name { get; private set; }

		public NamedInstance(object instance, ServiceName name)
		{
			Instance = instance;
			Name = name;
		}
	}
}