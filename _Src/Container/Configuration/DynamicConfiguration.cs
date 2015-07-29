using System;

namespace SimpleContainer.Configuration
{
	internal class DynamicConfiguration
	{
		public DynamicConfiguration(string description, Type baseType,
			Action<Type, ServiceConfigurationBuilder<object>> configureAction)
		{
			Description = description;
			BaseType = baseType;
			ConfigureAction = configureAction;
		}

		public string Description { get; private set; }
		public Type BaseType { get; private set; }
		public Action<Type, ServiceConfigurationBuilder<object>> ConfigureAction { get; private set; }
	}
}