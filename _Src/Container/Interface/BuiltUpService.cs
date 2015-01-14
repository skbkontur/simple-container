using SimpleContainer.Implementation;

namespace SimpleContainer.Interface
{
	public struct BuiltUpService
	{
		private readonly DependenciesInjector.Injection[] injections;

		internal BuiltUpService(DependenciesInjector.Injection[] injections)
		{
			this.injections = injections;
		}

		public void Run(bool dumpConstructionLog = false)
		{
			foreach (var injection in injections)
				injection.value.Run(dumpConstructionLog);
		}
	}
}