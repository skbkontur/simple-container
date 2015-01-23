using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Infection;
using SimpleContainer.Interface;

namespace SimpleContainer.Helpers
{
	internal static class AssemblyHelpers
	{
		public static IEnumerable<Assembly> Closure(this IEnumerable<Assembly> assemblies, Func<AssemblyName, bool> filter)
		{
			var assembliesArray = assemblies.ToArray();
			var result = new HashSet<Assembly>(assembliesArray);
			var referencesChain = new Stack<Assembly>();
			foreach (var assembly in assembliesArray.Where(x => filter(x.GetName())))
				ProcessAssembly(assembly, result, filter, referencesChain);
			return result.ToArray();
		}

		private static void ProcessAssembly(Assembly assembly, ISet<Assembly> result,
			Func<AssemblyName, bool> filter, Stack<Assembly> referencesChain)
		{
			referencesChain.Push(assembly);
			var referencedByAttribute = assembly.GetCustomAttributes<ContainerReferenceAttribute>()
				.Select(x => new AssemblyName(x.AssemblyName));
			var references = assembly.GetReferencedAssemblies()
				.Concat(referencedByAttribute)
				.Where(filter);
			foreach (var name in references)
			{
				Assembly referencedAssembly;
				try
				{
					referencedAssembly = LoadAssembly(name);
				}
				catch (Exception e)
				{
					const string messageFormat = "exception loading assembly [{0}], reference chain {1}";
					var referenceChain = referencesChain.Select(x => "[" + x.GetName().Name + "]").Reverse().JoinStrings("->");
					throw new SimpleContainerException(string.Format(messageFormat, name.Name, referenceChain), e);
				}
				if (result.Add(referencedAssembly))
					ProcessAssembly(referencedAssembly, result, filter, referencesChain);
			}
			referencesChain.Pop();
		}

		public static Assembly LoadAssembly(AssemblyName name)
		{
			try
			{
				return Assembly.Load(name);
			}
			catch (BadImageFormatException e)
			{
				const string messageFormat = "bad assembly image, assembly name [{0}], " +
				                             "process is [{1}],\r\nFusionLog\r\n{2}";
				throw new SimpleContainerException(string.Format(messageFormat,
					e.FileName, Environment.Is64BitProcess ? "x64" : "x86", e.FusionLog), e);
			}
		}
	}
}