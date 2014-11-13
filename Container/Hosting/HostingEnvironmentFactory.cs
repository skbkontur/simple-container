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
			this.assembliesFilter = assembliesFilter;
		}

		public HostingEnvironment Create(string directoryPath, bool withExecutables)
		{
			var assemblies = Directory.GetFiles(BasePath(), "*.dll")
				.Union(withExecutables ? Directory.GetFiles(BasePath(), "*.exe") : Enumerable.Empty<string>())
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
			return Create(assemblies);
		}

		private static string BasePath()
		{
			return String.IsNullOrEmpty(AppDomain.CurrentDomain.RelativeSearchPath)
				? AppDomain.CurrentDomain.BaseDirectory
				: AppDomain.CurrentDomain.RelativeSearchPath;
		}

		public HostingEnvironment Create(IEnumerable<Assembly> assemblies)
		{
			var types = LoadTypes(assemblies);
			return Create(types);
		}

		public HostingEnvironment Create(Type[] types)
		{
			var configuration = CreateDefaultConfiguration(types);
			var inheritors = DefaultInheritanceHierarchy.Create(types);
			return new HostingEnvironment(inheritors, configuration, assembliesFilter);
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