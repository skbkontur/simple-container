using System;

namespace SimpleContainer.Configuration
{
	internal interface IInternalConfigurationBuilder
	{
		void DontUse(Type type);
		void BindDependency(Type type, string dependencyName, object value);
		void Bind(Type interfaceType, Type implementationType);
	}
}