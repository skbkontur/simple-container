using System;
using System.Reflection;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

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
			ApplicationName = "global";
		}

		internal ConfigurationContext Local(string name, Assembly primaryAssembly)
		{
			return new ConfigurationContext(profile, settingsLoader)
			{
				ApplicationName = name,
				PrimaryAssembly = primaryAssembly
			};
		}

		public bool ProfileIs<T>()
			where T : IProfile
		{
			return profile == typeof (T);
		}

		public string ApplicationName { get; private set; }
		public Assembly PrimaryAssembly { get; private set; }

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