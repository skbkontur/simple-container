using System;
using System.Collections.Generic;
using SimpleContainer.Helpers;

namespace SimpleContainer.Configuration
{
	public abstract class ConfigurationRegistry
	{
		private readonly IDictionary<Type, object> configurations;

		protected ConfigurationRegistry(IDictionary<Type, object> configurations)
		{
			this.configurations = configurations;
		}

		public T GetOrNull<T>(Type type) where T : class
		{
			return configurations.GetOrDefault(type) as T;
		}
	}
}