using System;
using System.Reflection;
using SimpleContainer.Interface;

namespace SimpleContainer.Helpers
{
	internal static class AssemblyHelpers
	{
		public static Assembly LoadAssembly(AssemblyName name)
		{
			try
			{
				return Assembly.Load(name);
			}
			catch (BadImageFormatException e)
			{
				var message = $"bad assembly image, assembly name [{e.FileName}], process is [{(Environment.Is64BitProcess ? "x64" : "x86")}],{Environment.NewLine}FusionLog{Environment.NewLine}{e.FusionLog}";
				throw new SimpleContainerException(message, e);
			}
		}
	}
}