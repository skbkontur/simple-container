using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation;
using SimpleContainer.Interface;

namespace SimpleContainer
{
	public class ContainerFactory
	{
		private Type profile;
		private Func<AssemblyName, bool> assembliesFilter = n => true;
		private Func<Type, string, object> settingsLoader;
		private string configFileName;
		private LogError errorLogger;
		private LogInfo infoLogger;
		private Type[] priorities;
		private Action<ContainerConfigurationBuilder> configure;
		private BuildContext buildContext;
		private Func<Type[]> types;
		private IParametersSource parameters;
		private readonly Dictionary<Type, Func<object, string>> valueFormatters = new Dictionary<Type, Func<object, string>>();

		public ContainerFactory WithSettingsLoader(Func<Type, object> newLoader)
		{
			var cache = new ConcurrentDictionary<Type, object>();
			settingsLoader = (t, _) => cache.GetOrAdd(t, newLoader);
			return this;
		}

		public ContainerFactory WithSettingsLoader(Func<Type, string, object> newLoader)
		{
			var cache = new ConcurrentDictionary<string, object>();
			settingsLoader = (t, k) => cache.GetOrAdd(t.Name + k, s => newLoader(t, k));
			return this;
		}

		public ContainerFactory WithConfigurator(Action<ContainerConfigurationBuilder> newConfigure)
		{
			configure = newConfigure;
			return this;
		}

		public ContainerFactory WithParameters(IParametersSource newParameters)
		{
			parameters = newParameters;
			return this;
		}

		public ContainerFactory WithValueFormatter<T>(Func<T, string> formatter)
		{
			valueFormatters[typeof (T)] = o => formatter((T) o);
			buildContext = null;
			return this;
		}

		public ContainerFactory WithAssembliesFilter(Func<AssemblyName, bool> newAssembliesFilter)
		{
			assembliesFilter = n => newAssembliesFilter(n) || n.Name == "SimpleContainer";
			buildContext = null;
			return this;
		}

		public ContainerFactory WithPriorities(params Type[] newConfiguratorTypes)
		{
			priorities = newConfiguratorTypes;
			return this;
		}

		public ContainerFactory WithConfigFile(string fileName)
		{
			configFileName = fileName;
			buildContext = null;
			return this;
		}

		public ContainerFactory WithErrorLogger(LogError logger)
		{
			errorLogger = logger;
			buildContext = null;
			return this;
		}

		public ContainerFactory WithInfoLogger(LogInfo logger)
		{
			infoLogger = logger;
			buildContext = null;
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

		public ContainerFactory WithTypesFromDefaultBinDirectory(bool withExecutables)
		{
			return WithTypesFromDirectory(GetBinDirectory(), withExecutables);
		}

		public ContainerFactory WithTypesFromDirectory(string directory, bool withExecutables)
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
			return WithTypesFromAssemblies(assemblies);
		}

		public ContainerFactory WithTypesFromAssemblies(IEnumerable<Assembly> assemblies)
		{
			return WithTypesFromAssemblies(assemblies.AsParallel());
		}

		public ContainerFactory WithTypes(Type[] newTypes)
		{
			types = () => newTypes;
			buildContext = null;
			return this;
		}

		public IContainer Build()
		{
			var containerBuildContext = buildContext ?? (buildContext = CreateBuildContext());
			var builder = new ContainerConfigurationBuilder();
			var configurationContext = new ConfigurationContext(profile, settingsLoader, parameters);
			containerBuildContext.configuratorRunner.Run(builder, configurationContext, priorities);
			if (configure != null)
				configure(builder);
			if (containerBuildContext.fileConfigurator != null)
				containerBuildContext.fileConfigurator(builder);
			return CreateContainer(containerBuildContext, builder.RegistryBuilder.Build(containerBuildContext.inheritors));
		}

		private IContainer CreateContainer(BuildContext currentBuildContext, IConfigurationRegistry configuration)
		{
			return new Implementation.SimpleContainer(currentBuildContext.genericsAutoCloser, configuration,
				currentBuildContext.inheritors, errorLogger, infoLogger, valueFormatters);
		}

		private static string GetBinDirectory()
		{
			return String.IsNullOrEmpty(AppDomain.CurrentDomain.RelativeSearchPath)
				? AppDomain.CurrentDomain.BaseDirectory
				: AppDomain.CurrentDomain.RelativeSearchPath;
		}

		private ContainerFactory WithTypesFromAssemblies(ParallelQuery<Assembly> assemblies)
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
			buildContext = null;
			return this;
		}

		private BuildContext CreateBuildContext()
		{
			var result = new BuildContext
			{
				types = types().Concat(Assembly.GetExecutingAssembly().GetTypes()).Distinct().ToArray()
			};
			result.inheritors = InheritorsBuilder.CreateInheritorsMap(result.types);
			result.genericsAutoCloser = new GenericsAutoCloser(result.inheritors, assembliesFilter);
			if (configFileName != null && File.Exists(configFileName))
				result.fileConfigurator = FileConfigurationParser.Parse(result.types, configFileName);
			var configurationContainer = CreateContainer(result, EmptyConfigurationRegistry.Instance);
			result.configuratorRunner = configurationContainer.Get<ConfiguratorRunner>();
			return result;
		}

		private class BuildContext
		{
			public Type[] types;
			public Dictionary<Type, List<Type>> inheritors;
			public GenericsAutoCloser genericsAutoCloser;
			public Action<ContainerConfigurationBuilder> fileConfigurator;
			public ConfiguratorRunner configuratorRunner;
		}
	}
}