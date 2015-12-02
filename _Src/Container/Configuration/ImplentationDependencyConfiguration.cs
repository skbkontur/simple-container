using System;
using SimpleContainer.Implementation;

namespace SimpleContainer.Configuration
{
	internal class ImplentationDependencyConfiguration
	{
		private ImplentationDependencyConfiguration()
		{
		}

		public bool Used { get; set; }
		public object Value { get; private set; }
		public bool ValueAssigned { get; private set; }
		public Type ImplementationType { get; private set; }
		public Func<ContainerService.Builder, object> Factory { get; private set; }

		public class Builder
		{
			private readonly ImplentationDependencyConfiguration target = new ImplentationDependencyConfiguration();

			public void UseValue(object o)
			{
				target.Value = o;
				target.ValueAssigned = true;
			}

			public void UseFactory(Func<IContainer, object> creator)
			{
				target.Factory = b => creator(b.Context.Container);
			}

			public void UseImplementation(Type type)
			{
				target.ImplementationType = type;
			}

			public ImplentationDependencyConfiguration Build()
			{
				return target;
			}
		}
	}
}