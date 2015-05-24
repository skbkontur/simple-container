using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SimpleContainer.Configuration;
using SimpleContainer.Generics;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation;
using SimpleContainer.Interface;

namespace SimpleContainer
{
	public class ContainerFactory
	{
		private Type profile;
		private Func<AssemblyName, bool> assembliesFilter;
		private Func<Type, object> settingsLoader;
		private string configFileName;
		private LogError errorLogger;
		private LogInfo infoLogger;
		private readonly List<Assembly> pluginAssemblies = new List<Assembly>();
		private Type[] configuratorTypes;

		public ContainerFactory WithSettingsLoader(Func<Type, object> newLoader)
		{
			var cache = new ConcurrentDictionary<Type, object>();
			settingsLoader = t => cache.GetOrAdd(t, newLoader);
			return this;
		}

		public ContainerFactory WithAssembliesFilter(Func<AssemblyName, bool> newAssembliesFilter)
		{
			assembliesFilter = name => newAssembliesFilter(name) || name.Name == "SimpleContainer";
			return this;
		}

		public ContainerFactory WithPriorities(params Type[] newConfiguratorTypes)
		{
			configuratorTypes = newConfiguratorTypes;
			return this;
		}

		public ContainerFactory WithPlugin(Assembly assembly)
		{
			pluginAssemblies.Add(assembly);
			return this;
		}

		public ContainerFactory WithConfigFile(string fileName)
		{
			configFileName = fileName;
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

		public IStaticContainer FromDefaultBinDirectory(bool withExecutables)
		{
			return FromDirectory(GetBinDirectory(), withExecutables);
		}

		public IStaticContainer FromDirectory(string directory, bool withExecutables)
		{
			var assemblies = Directory.GetFiles(directory, "*.dll")
				.Union(withExecutables ? Directory.GetFiles(directory, "*.exe") : Enumerable.Empty<string>())
				.Select(delegate(string s)
				{
					try
					{
						return AssemblyName.GetAssemblyName(s);
					}
					catch (BadImageFormatException)
					{
						return null;
					}
				})
				.NotNull()
				.Where(assembliesFilter)
				.AsParallel()
				.Select(AssemblyHelpers.LoadAssembly);
			return FromAssemblies(assemblies);
		}

		public IStaticContainer FromAssemblies(IEnumerable<Assembly> assemblies)
		{
			return FromAssemblies(assemblies.AsParallel());
		}

		private IStaticContainer FromAssemblies(ParallelQuery<Assembly> assemblies)
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
			var defaultTypes = pluginAssemblies.Concat(Assembly.GetExecutingAssembly()).SelectMany(x => x.GetTypes());
			var hostingTypes = types.Concat(defaultTypes).Distinct().ToArray();
			var configuration = CreateDefaultConfiguration(hostingTypes);
			var inheritors = DefaultInheritanceHierarchy.Create(hostingTypes);

			var staticServices = new HashSet<Type>();
			var builder = new ContainerConfigurationBuilder(staticServices, true);
			var configurationContext = new ConfigurationContext(profile, settingsLoader);
			using (
				var runner = ConfiguratorRunner.Create(true, configuration, inheritors, configurationContext, configuratorTypes))
				runner.Run(builder);
			var containerConfiguration = new MergedConfiguration(configuration, builder.RegistryBuilder.Build());
			var fileConfigurator = File.Exists(configFileName) ? FileConfigurationParser.Parse(types, configFileName) : null;
			return new StaticContainer(containerConfiguration, inheritors, assembliesFilter,
				configurationContext, staticServices, fileConfigurator, errorLogger, infoLogger, pluginAssemblies, configuratorTypes);
		}

		public IStaticContainer FromCurrentAppDomain()
		{
			return FromAssemblies(AppDomain.CurrentDomain.GetAssemblies().Where(x => !x.IsDynamic).Closure(assembliesFilter));
		}

		private static string GetBinDirectory()
		{
			return String.IsNullOrEmpty(AppDomain.CurrentDomain.RelativeSearchPath)
				? AppDomain.CurrentDomain.BaseDirectory
				: AppDomain.CurrentDomain.RelativeSearchPath;
		}

		private IConfigurationRegistry CreateDefaultConfiguration(Type[] types)
		{
			var genericsProcessor = new GenericsConfigurationProcessor(assembliesFilter);
			var builder = new ConfigurationRegistry.Builder();
			foreach (var type in types)
				genericsProcessor.FirstRun(type);
			foreach (var type in types)
				genericsProcessor.SecondRun(builder, type);
			return builder.Build();
		}
	}
}