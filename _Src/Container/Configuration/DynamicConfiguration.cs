using System;

namespace SimpleContainer.Configuration
{
	internal class DynamicConfiguration
	{
		public Func<Type, bool> Filter { get; set; }
		public Type BaseType { get; set; }
		public Action<Type, ServiceConfigurationBuilder<object>> ConfigureAction { get; set; }
	}
}