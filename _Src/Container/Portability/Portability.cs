using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SimpleContainer
{
    public static class Portability
    {
        public static AssemblyName GetAssemblyName(string file)
        {
#if NETCORE1
            return System.Runtime.Loader.AssemblyLoadContext.GetAssemblyName(file);
#else
            return AssemblyName.GetAssemblyName(file);
#endif
        }

        public static Assembly GetSimpleContainerAssembly()
        {
#if NETCORE1
            return typeof(Portability).GetTypeInfo().Assembly;
#else
            return Assembly.GetExecutingAssembly();
#endif
        }
    }
}