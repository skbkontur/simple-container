#if NETCORE1

namespace System
{
    public class AppDomain
    {
        static Lazy<AppDomain> _currentDomain = new Lazy<AppDomain>(() => new AppDomain());
        public static AppDomain CurrentDomain
        {
            get { return _currentDomain.Value; }
        }

        public string BaseDirectory
        {
            get
            {
                return AppContext.BaseDirectory;
            }
        }

        public string RelativeSearchPath
        {
            get { return null; }
        }
    }
}

#endif
