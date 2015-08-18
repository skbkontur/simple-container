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
				const string messageFormat = "bad assembly image, assembly name [{0}], " +
				                             "process is [{1}],\r\nFusionLog\r\n{2}";
				throw new SimpleContainerException(string.Format(messageFormat,
					e.FileName, Environment.Is64BitProcess ? "x64" : "x86", e.FusionLog), e);
			}
		}
	}
}