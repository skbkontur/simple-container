using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Implementation;

namespace SimpleContainer.Interface
{
	public struct ResolvedService<T>
	{
		private ResolvedService resolvedService;

		internal ResolvedService(ResolvedService resolvedService)
		{
			this.resolvedService = resolvedService;
		}

		public void Run(bool dumpConstructionLog = false)
		{
			resolvedService.Run(dumpConstructionLog);
		}

		public T Single()
		{
			return (T) resolvedService.Single();
		}

		public IEnumerable<T> All()
		{
			return resolvedService.All().Cast<T>();
		}

		public string GetConstructionLog(bool entireResolutionContext = false)
		{
			return resolvedService.GetConstructionLog(entireResolutionContext);
		}
	}

	public struct ResolvedService
	{
		private readonly ContainerService containerService;
		private readonly Implementation.SimpleContainer simpleContainer;
		private readonly bool isEnumerable;

		internal ResolvedService(ContainerService containerService, Implementation.SimpleContainer simpleContainer,
			bool isEnumerable)
		{
			this.containerService = containerService;
			this.simpleContainer = simpleContainer;
			this.isEnumerable = isEnumerable;
		}

		public void Run(bool dumpConstructionLog = false)
		{
			simpleContainer.Run(containerService, dumpConstructionLog ? GetConstructionLog() : null);
		}

		public object Single()
		{
			return isEnumerable ? All() : containerService.SingleInstance(false);
		}

		public IEnumerable<object> All()
		{
			return containerService.AsEnumerable();
		}

		public void DumpConstructionLog(ISimpleLogWriter writer, bool entireResolutionContext = false)
		{
			containerService.Context.Format(entireResolutionContext ? null : containerService, writer);
		}

		public string GetConstructionLog(bool entireResolutionContext = false)
		{
			var logWriter = new SimpleTextLogWriter();
			DumpConstructionLog(logWriter, entireResolutionContext);
			return logWriter.GetText();
		}
	}
}