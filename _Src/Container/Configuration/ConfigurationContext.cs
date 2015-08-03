using System;
using System.Collections.Generic;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Configuration
{
	public class ConfigurationContext
	{
		private readonly Type profile;
		private readonly Func<Type, string, object> settingsLoader;

		internal ConfigurationContext(Type profile, Func<Type, string, object> settingsLoader, IParametersSource parameters)
		{
			this.profile = profile;
			this.settingsLoader = settingsLoader;
			Parameters = parameters ?? new EmptyParametersSource();
		}

		private class EmptyParametersSource : IParametersSource
		{
			public IEnumerable<string> Names
			{
				get { return new String[0]; }
			}

			public bool TryGet(string name, Type type, out object value)
			{
				value = null;
				return false;
			}
		}

		public bool ProfileIs<T>()
			where T : IProfile
		{
			return profile == typeof (T);
		}

		public IParametersSource Parameters { get; private set; }

		public T Settings<T>(string key = null)
		{
			if (settingsLoader == null)
			{
				const string message = "settings loader is not configured, use ContainerFactory.WithSettingsLoader";
				throw new SimpleContainerException(message);
			}
			var settingsInstance = settingsLoader(typeof (T), key);
			if (settingsInstance == null)
			{
				const string messageFormat = "settings loader returned null for type [{0}]";
				throw new SimpleContainerException(string.Format(messageFormat, typeof (T).FormatName()));
			}
			if (settingsInstance is T == false)
			{
				const string messageFormat = "invalid settings type, required [{0}], actual [{1}]";
				throw new SimpleContainerException(string.Format(messageFormat, typeof (T).FormatName(),
					settingsInstance.GetType().FormatName()));
			}
			return (T) settingsInstance;
		}
	}
}