using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SimpleContainer.Factories;
using SimpleContainer.Generics;
using SimpleContainer.Helpers;

namespace SimpleContainer.Hosting
{
	public class HostingEnvironmentFactory
	{
		private readonly Func<AssemblyName, bool> assembliesFilter;

		public HostingEnvironmentFactory(Func<AssemblyName, bool> assembliesFilter)
		{
			this.assembliesFilter = name => assembliesFilter(name) || name.Name == "SimpleContainer";
		}

		public HostingEnvironment FromDefaultBinDirectory(bool withExecutables)
		{
			return FromDirectory(GetBinDirectory(), withExecutables);
		}

		public HostingEnvironment FromDirectory(string directory, bool withExecutables)
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

		public HostingEnvironment FromAssemblies(IEnumerable<Assembly> assemblies)
		{
			var types = LoadTypes(assemblies);
			return FromTypes(types);
		}

		public HostingEnvironment FromTypes(Type[] types)
		{
			var hostingTypes = types.Concat(Assembly.GetExecutingAssembly().GetTypes()).ToArray();
			var configuration = CreateDefaultConfiguration(hostingTypes);
			var inheritors = DefaultInheritanceHierarchy.Create(hostingTypes);
			return new HostingEnvironment(inheritors, configuration, assembliesFilter);
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
			var builder = new ContainerConfigurationBuilder();
			foreach (var type in types)
			{
				genericsProcessor.FirstRun(type);
				factoriesProcessor.FirstRun(builder, type);
			}
			foreach (var type in types)
				genericsProcessor.SecondRun(builder, type);
			return builder.Build();
		}

		private static Type[] LoadTypes(IEnumerable<Assembly> assemblies)
		{
			try
			{
				return assemblies.SelectMany(x => x.GetTypes()).ToArray();
			}
			catch (ReflectionTypeLoadException typeLoadException)
			{
				throw new SimpleContainerTypeLoadException(typeLoadException);
			}
		}
	}
}