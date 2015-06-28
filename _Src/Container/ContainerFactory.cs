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
		private Func<AssemblyName, bool> assembliesFilter = n => n.Name == "SimpleContainer";
		private Func<Type, string, object> settingsLoader;
		private string configFileName;
		private LogError errorLogger;
		private LogInfo infoLogger;
		private readonly List<Assembly> pluginAssemblies = new List<Assembly>();
		private Type[] priorities;
		private Action<ContainerConfigurationBuilder> configure;
		private Type[] types;
		private IParametersSource parameters;

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

		public ContainerFactory WithAssembliesFilter(Func<AssemblyName, bool> newAssembliesFilter)
		{
			assembliesFilter = n => newAssembliesFilter(n) || n.Name == "SimpleContainer";
			return this;
		}

		public ContainerFactory WithPriorities(params Type[] newConfiguratorTypes)
		{
			priorities = newConfiguratorTypes;
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
			types = newTypes;
			return this;
		}

		public IContainer Build()
		{
			var defaultTypes = pluginAssemblies.Concat(Assembly.GetExecutingAssembly()).SelectMany(x => x.GetTypes());
			var hostingTypes = types.Concat(defaultTypes).Distinct().ToArray();
			var inheritors = DefaultInheritanceHierarchy.Create(hostingTypes);
			var genericsAutoCloser = new GenericsAutoCloser(inheritors, assembliesFilter);
			var builder = new ContainerConfigurationBuilder();
			var configurationContext = new ConfigurationContext(profile, settingsLoader, parameters);
			using (var container = CreateContainer(inheritors, genericsAutoCloser, EmptyConfigurationRegistry.Instance))
			{
				var runner = container.Get<ConfiguratorRunner>();
				runner.Run(builder, configurationContext, priorities);
			}
			if (configure != null)
				configure(builder);
			var fileConfigurator = File.Exists(configFileName) ? FileConfigurationParser.Parse(types, configFileName) : null;
			if (fileConfigurator != null)
				fileConfigurator(_ => true, builder);
			return CreateContainer(inheritors, genericsAutoCloser, builder.RegistryBuilder.Build());
		}

		private IContainer CreateContainer(IInheritanceHierarchy inheritors, GenericsAutoCloser genericsAutoCloser,
			IConfigurationRegistry configuration)
		{
			return new Implementation.SimpleContainer(genericsAutoCloser, configuration, inheritors, errorLogger, infoLogger);
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
				})
				.ToArray();
			return WithTypes(newTypes);
		}
	}
}