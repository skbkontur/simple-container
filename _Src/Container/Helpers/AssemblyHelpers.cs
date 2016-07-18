using System;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
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
#if NETCORE1
				const string messageFormat = "bad assembly image, assembly name [{0}], process is [{1}]";
				throw new SimpleContainerException(string.Format(messageFormat,
					e.FileName, RuntimeInformation.ProcessArchitecture), e);
#else
				const string messageFormat = "bad assembly image, assembly name [{0}], " +
				                             "process is [{1}],\r\nFusionLog\r\n{2}";
				throw new SimpleContainerException(string.Format(messageFormat,
					e.FileName, RuntimeInformation.ProcessArchitecture, e.FusionLog), e);
#endif
			}
		}
	}
}