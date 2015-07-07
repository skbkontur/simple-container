using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SimpleContainer.Configuration;
using SimpleContainer.Factories;
using SimpleContainer.Generics;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation;
using SimpleContainer.Implementation.Hacks;
using SimpleContainer.Interface;

namespace SimpleContainer
{
	public class ContainerFactory
	{
		private Type profile;
		private Func<AssemblyName, bool> assembliesFilter;
		private Func<Type, object> settingsLoader;
		private LogError errorLogger;
		private LogInfo infoLogger;
		private readonly List<Assembly> pluginAssemblies = new List<Assembly>();

		public ContainerFactory WithSettingsLoader(Func<Type, object> newLoader)
		{
			var cache = new NonConcurrentDictionary<Type, object>();
			settingsLoader = t => cache.GetOrAdd(t, newLoader);
			return this;
		}

		public ContainerFactory WithAssembliesFilter(Func<AssemblyName, bool> newAssembliesFilter)
		{
			assembliesFilter = name => newAssembliesFilter(name) || name.Name == "SimpleContainer";
			return this;
		}

		public ContainerFactory WithPlugin(Assembly assembly)
		{
			pluginAssemblies.Add(assembly);
			return this;
		}

		public ContainerFactory WithErrorLogger(LogError logger)
		{
			errorLogger = logger;
			return this;
		}

		public ContainerFactory WithInfoLogger(LogInfo logger)
		{
			infoLogger = logger;
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

		public IStaticContainer FromAssemblies(IEnumerable<Assembly> assemblies)
		{
			var types = assemblies
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
				})
				.ToArray();
			return FromTypes(types);
		}

		public IStaticContainer FromTypes(Type[] types)
		{
			var defaultTypes = pluginAssemblies.Concat(typeof(ContainerFactory).GetTypeInfo().Assembly).SelectMany(x => x.GetTypes());
			var hostingTypes = types.Concat(defaultTypes).Distinct().ToArray();
			var configuration = CreateDefaultConfiguration(hostingTypes);
			var inheritors = DefaultInheritanceHierarchy.Create(hostingTypes);
			var genericsAutocloser = new GenericsAutoCloser(((DefaultInheritanceHierarchy) inheritors).GetImpl(),assembliesFilter);
			var staticServices = new HashSet<Type>();
			var builder = new ContainerConfigurationBuilder(staticServices, true);
			var configurationContext = new ConfigurationContext(profile, settingsLoader);
			using (var runner = ConfiguratorRunner.Create(true, configuration, inheritors, configurationContext, genericsAutocloser))
				runner.Run(builder, x => true);
			var containerConfiguration = new MergedConfiguration(configuration, builder.Build());
			return new StaticContainer(containerConfiguration, inheritors, assembliesFilter,
				configurationContext, staticServices, null, errorLogger, infoLogger, pluginAssemblies, genericsAutocloser);
		}

		private IContainerConfiguration CreateDefaultConfiguration(Type[] types)
		{
			var factoriesProcessor = new FactoryConfigurationProcessor();
			var builder = new ContainerConfigurationBuilder(new HashSet<Type>(), false);
			foreach (var type in types)
				factoriesProcessor.FirstRun(builder, type);
			return builder.Build();
		}
	}
}