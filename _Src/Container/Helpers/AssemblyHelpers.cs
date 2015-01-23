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
			return assemblies
				.Select(x => new ReferencedAssembly(x))
				.Closure(x =>
				{
					try
					{
						return x.Assembly ?? LoadAssembly(x.Name);
					}
					catch (Exception e)
					{
						const string messageFormat = "exception loading assembly {0}";
						throw new SimpleContainerException(string.Format(messageFormat, x.FormatName()), e);
					}
				}, (name, assembly) => assembly.ReferencedAssemblyNames(filter).Select(n => new ReferencedAssembly(n, name)));
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

		private static IEnumerable<AssemblyName> ReferencedAssemblyNames(this Assembly assembly,
			Func<AssemblyName, bool> assemblyFilter)
		{
			var referencedByAttribute = assembly.GetCustomAttributes<ContainerReferenceAttribute>()
				.Select(x => new AssemblyName(x.AssemblyName));
			return assembly.GetReferencedAssemblies()
				.Concat(referencedByAttribute)
				.Where(assemblyFilter);
		}

		private class ReferencedAssembly
		{
			public AssemblyName Name { get; private set; }
			public Assembly Assembly { get; private set; }
			private readonly ReferencedAssembly referencedBy;

			public ReferencedAssembly(AssemblyName name, ReferencedAssembly referencedBy)
			{
				Name = name;
				this.referencedBy = referencedBy;
			}

			public ReferencedAssembly(Assembly assembly)
			{
				Assembly = assembly;
				Name = assembly.GetName();
			}

			public string FormatName()
			{
				return ReferenceChain().Reverse().Select(x => "[" + x.Name + "]").JoinStrings("->");
			}

			private bool Equals(ReferencedAssembly other)
			{
				return Name.Equals(other.Name);
			}

			public override bool Equals(object obj)
			{
				if (ReferenceEquals(null, obj)) return false;
				if (ReferenceEquals(this, obj)) return true;
				if (obj.GetType() != GetType()) return false;
				return Equals((ReferencedAssembly) obj);
			}

			public override int GetHashCode()
			{
				return Name.GetHashCode();
			}

			private IEnumerable<AssemblyName> ReferenceChain()
			{
				var current = this;
				while (current != null)
				{
					yield return current.Name;
					current = current.referencedBy;
				}
			}
		}
	}
}