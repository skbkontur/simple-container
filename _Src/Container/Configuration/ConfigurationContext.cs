using System;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation;

namespace SimpleContainer.Configuration
{
	public class ConfigurationContext
	{
		private readonly Type profile;
		private readonly Func<Type, object> settingsLoader;

		internal ConfigurationContext(Type profile, Func<Type, object> settingsLoader)
		{
			this.profile = profile;
			this.settingsLoader = settingsLoader;
		}

		public bool ProfileIs<T>()
		{
			return profile == typeof (T);
		}

		public T Settings<T>()
		{
			if (settingsLoader == null)
			{
				const string message = "settings loader is not configured, use ContainerFactory.WithSettingsLoader";
				throw new SimpleContainerException(message);
			}
			var settingsInstance = settingsLoader(typeof (T));
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