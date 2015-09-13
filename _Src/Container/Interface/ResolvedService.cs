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

		public ServiceName Name
		{
			get { return resolvedService.Name; }
		}

		public void Run(bool dumpConstructionLog = false)
		{
			resolvedService.Run(dumpConstructionLog);
		}

		public bool IsOk()
		{
			return resolvedService.IsOk();
		}

		public void CheckSingleInstance()
		{
			resolvedService.CheckSingleInstance();
		}

		public T Single()
		{
			return (T) resolvedService.Single();
		}

		public T SingleOrDefault(T defaultValue = default(T))
		{
			return (T) resolvedService.SingleODefault(defaultValue);
		}

		public IEnumerable<T> All()
		{
			return resolvedService.All().Cast<T>();
		}

		public string GetConstructionLog()
		{
			return resolvedService.GetConstructionLog();
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

		public ServiceName Name
		{
			get { return new ServiceName(containerService.Type, containerService.UsedContracts); }
		}

		public void Run(bool dumpConstructionLog = false)
		{
			simpleContainer.Run(containerService, dumpConstructionLog ? GetConstructionLog() : null);
		}

		public void CheckSingleInstance()
		{
			Single();
		}

		public object Single()
		{
			return isEnumerable ? All() : containerService.GetSingleValue(false, null);
		}

		public object SingleODefault(object defaultValue)
		{
			return isEnumerable ? All() : containerService.GetSingleValue(true, defaultValue);
		}

		public bool IsOk()
		{
			return containerService.Status == ServiceStatus.Ok;
		}

		public IEnumerable<object> All()
		{
			return containerService.GetAllValues();
		}

		public void DumpConstructionLog(ISimpleLogWriter writer)
		{
			containerService.WriteConstructionLog(writer);
		}

		public string GetConstructionLog()
		{
			var logWriter = new SimpleTextLogWriter();
			DumpConstructionLog(logWriter);
			return logWriter.GetText();
		}
	}
}