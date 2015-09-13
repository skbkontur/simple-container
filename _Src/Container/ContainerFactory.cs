using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation;
using SimpleContainer.Implementation.Hacks;
using SimpleContainer.Interface;

namespace SimpleContainer
{
	public class ContainerFactory
	{
		private Type profile;
		private Func<AssemblyName, bool> assembliesFilter = n => true;
		private Func<Type, string, object> settingsLoader;
		private LogError errorLogger;
		private LogInfo infoLogger;
		private Type[] priorities;
		private Action<ContainerConfigurationBuilder> configure;
		private TypesContext typesContextCache;
		private Func<Type[]> types;
		private IParametersSource parameters;
		private readonly Dictionary<Type, Func<object, string>> valueFormatters = new Dictionary<Type, Func<object, string>>();

		private readonly Dictionary<Type, ConfigurationRegistry> configurationByProfileCache =
			new Dictionary<Type, ConfigurationRegistry>();

		public ContainerFactory WithSettingsLoader(Func<Type, object> newLoader)
		{
			var cache = new NonConcurrentDictionary<Type, object>();
			settingsLoader = (t, _) => cache.GetOrAdd(t, newLoader);
			configurationByProfileCache.Clear();
			return this;
		}

		public ContainerFactory WithSettingsLoader(Func<Type, string, object> newLoader)
		{
			var cache = new NonConcurrentDictionary<string, object>();
			settingsLoader = (t, k) => cache.GetOrAdd(t.Name + k, s => newLoader(t, k));
			configurationByProfileCache.Clear();
			return this;
		}

		public ContainerFactory WithParameters(IParametersSource newParameters)
		{
			parameters = newParameters;
			configurationByProfileCache.Clear();
			return this;
		}

		public ContainerFactory WithValueFormatter<T>(Func<T, string> formatter)
		{
			valueFormatters[typeof (T)] = o => formatter((T) o);
			typesContextCache = null;
			configurationByProfileCache.Clear();
			return this;
		}

		public ContainerFactory WithAssembliesFilter(Func<AssemblyName, bool> newAssembliesFilter)
		{
			assembliesFilter = n => newAssembliesFilter(n) || n.Name == "SimpleContainer";
			typesContextCache = null;
			configurationByProfileCache.Clear();
			return this;
		}

		public ContainerFactory WithPriorities(params Type[] newConfiguratorTypes)
		{
			priorities = newConfiguratorTypes;
			configurationByProfileCache.Clear();
			return this;
		}

		public ContainerFactory WithErrorLogger(LogError logger)
		{
			errorLogger = logger;
			typesContextCache = null;
			configurationByProfileCache.Clear();
			return this;
		}

		public ContainerFactory WithInfoLogger(LogInfo logger)
		{
			infoLogger = logger;
			typesContextCache = null;
			configurationByProfileCache.Clear();
			return this;
		}

		public ContainerFactory WithProfile(Type newProfile)
		{
			if (newProfile != null && !typeof (IProfile).IsAssignableFrom(newProfile))
				throw new SimpleContainerException(string.Format("profile type [{0}] must inherit from IProfile",
					newProfile.FormatName()));
			profile = newProfile;
			return this;
		}

		public ContainerFactory WithConfigurator(Action<ContainerConfigurationBuilder> newConfigure)
		{
			configure = newConfigure;
			return this;
		}

		public ContainerFactory WithTypes(Type[] newTypes)
		{
			types = () => newTypes;
			typesContextCache = null;
			configurationByProfileCache.Clear();
			return this;
		}

		public IContainer Build()
		{
			var typesContext = typesContextCache;
			if (typesContext == null)
			{
				typesContext = new TypesContext
				{
					typesList = TypesList.Create(types().Concat(typeof(ContainerFactory).GetTypeInfo().Assembly.GetTypes()).Distinct().ToArray())
				};
				typesContext.genericsAutoCloser = new GenericsAutoCloser(typesContext.typesList, assembliesFilter);
				var configurationContainer = CreateContainer(typesContext, ConfigurationRegistry.Empty);
				typesContext.configuratorRunner = configurationContainer.Get<ConfiguratorRunner>();
				typesContextCache = typesContext;
			}
			ConfigurationRegistry configurationRegistry;
			if (!configurationByProfileCache.TryGetValue(profile ?? typeof (ContainerFactory), out configurationRegistry))
			{
				var builder = new ContainerConfigurationBuilder();
				var configurationContext = new ConfigurationContext(profile, settingsLoader, parameters);
				typesContext.configuratorRunner.Run(builder, configurationContext, priorities);
				if (typesContext.fileConfigurator != null)
					typesContext.fileConfigurator(builder);
				configurationRegistry = builder.RegistryBuilder.Build(typesContext.typesList, null);
				configurationByProfileCache.Add(profile ?? typeof (ContainerFactory), configurationRegistry);
			}
			return CreateContainer(typesContext, configurationRegistry.Apply(typesContext.typesList, configure));
		}

		private IContainer CreateContainer(TypesContext currentTypesContext, ConfigurationRegistry configuration)
		{
			return new Implementation.SimpleContainer(currentTypesContext.genericsAutoCloser, configuration,
				currentTypesContext.typesList, errorLogger, infoLogger, valueFormatters);
		}

		public ContainerFactory WithTypesFromAssemblies(IEnumerable<Assembly> assemblies)
		{
			var newTypes = assemblies
				.Where(x => assembliesFilter(x.GetName()))
				.SelectMany(a =>
				{
					try
					{
						return a.GetTypes();
					}
					catch (ReflectionTypeLoadException e)
					{
						const string messageFormat = "can't load types from assembly [{0}], loaderExceptions:\r\n{1}";
						var loaderExceptionsText = e.LoaderExceptions.Select(ex => ex.ToString()).JoinStrings("\r\n");
						throw new SimpleContainerException(string.Format(messageFormat, a.GetName(), loaderExceptionsText), e);
					}
				});
			types = newTypes.ToArray;
			typesContextCache = null;
			return this;
		}

		private class TypesContext
		{
			public TypesList typesList;
			public GenericsAutoCloser genericsAutoCloser;
			public Action<ContainerConfigurationBuilder> fileConfigurator;
			public ConfiguratorRunner configuratorRunner;
		}
	}
}