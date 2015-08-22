using System;
using System.IO;
using System.Linq;
using System.Reflection;
using SimpleContainer.Helpers;

namespace SimpleContainer.Tests.Helpers
{
	internal static class PclContainerFactoryExtensions
	{
		public static ContainerFactory WithTypesFromDefaultBinDirectory(this ContainerFactory containerFactory,
			bool _)
		{
			var assemblies = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory)
				.Where(file => Path.GetExtension(file).EqualsIgnoringCase(".dll"))
				.Select(Assembly.LoadFile);
			return containerFactory.WithTypesFromAssemblies(assemblies);
		}
	}
}