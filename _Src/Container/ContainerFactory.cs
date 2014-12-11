using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SimpleContainer.Configuration;
using SimpleContainer.Factories;
using SimpleContainer.Generics;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation;

namespace SimpleContainer
{
	public class ContainerFactory
	{
		private Type profile;
		private Func<AssemblyName, bool> assembliesFilter;
		private Func<Type, object> settingsLoader;

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

		public ContainerFactory WithProfile(Type newProfile)
		{
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
				.Select(Assembly.Load)
				.Distinct();
			return FromAssemblies(assemblies);
		}

		public IStaticContainer FromAssemblies(IEnumerable<Assembly> assemblies)
		{
			var types = LoadTypes(assemblies);
			return FromTypes(types);
		}

		public IStaticContainer FromTypes(Type[] types)
		{
			var hostingTypes = types.Concat(Assembly.GetExecutingAssembly().GetTypes()).Distinct().ToArray();
			var configuration = CreateDefaultConfiguration(hostingTypes);
			var inheritors = DefaultInheritanceHierarchy.Create(hostingTypes);

			var staticServices = new HashSet<Type>();
			var builder = new ContainerConfigurationBuilder(staticServices, true);
			var configurationContext = new ConfigurationContext(profile, settingsLoader);
			using (var runner = ConfiguratorRunner.Create(true, configuration, inheritors, configurationContext))
				runner.Run(builder, x => true);
			var containerConfiguration = new MergedConfiguration(configuration, builder.Build());
			return new StaticContainer(containerConfiguration, inheritors, assembliesFilter, configurationContext, staticServices);
		}

		public IStaticContainer FromCurrentAppDomain()
		{
			return FromAssemblies(AppDomain.CurrentDomain.GetAssemblies());
		}

		private static string GetBinDirectory()
		{
			return String.IsNullOrEmpty(AppDomain.CurrentDomain.RelativeSearchPath)
				? AppDomain.CurrentDomain.BaseDirectory
				: AppDomain.CurrentDomain.RelativeSearchPath;
		}

		private IContainerConfiguration CreateDefaultConfiguration(Type[] types)
		{
			var genericsProcessor = new GenericsConfigurationProcessor(assembliesFilter);
			var factoriesProcessor = new FactoryConfigurationProcessor();
			var builder = new ContainerConfigurationBuilder(new HashSet<Type>(), false);
			foreach (var type in types)
			{
				genericsProcessor.FirstRun(type);
				factoriesProcessor.FirstRun(builder, type);
			}
			foreach (var type in types)
				genericsProcessor.SecondRun(builder, type);
			return builder.Build();
		}

		private Type[] LoadTypes(IEnumerable<Assembly> assemblies)
		{
			return assemblies
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
						throw new SimpleContainerException(string.Format(messageFormat, a.GetName().Name, loaderExceptionsText), e);
					}
				})
				.ToArray();
		}
	}
}