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

		public void EnsureInitialized()
		{
			resolvedService.EnsureInitialized();
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
			return (T) resolvedService.SingleOrDefault(defaultValue);
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
		private readonly ContainerContext containerContext;
		private readonly bool isEnumerable;

		internal ResolvedService(ContainerService containerService, ContainerContext containerContext,
			bool isEnumerable)
		{
			this.containerService = containerService;
			this.containerContext = containerContext;
			this.isEnumerable = isEnumerable;
		}

		public ServiceName Name
		{
			get { return containerService.Name; }
		}

		public void EnsureInitialized()
		{
			containerService.EnsureInitialized(containerContext, containerService);
		}

		public void CheckSingleInstance()
		{
			Single();
		}

		public object Single()
		{
			containerService.CheckStatusIsGood(containerContext);
			if (isEnumerable)
				return containerService.GetAllValues();
			containerService.CheckSingleValue(containerContext);
			return containerService.Instances[0].Instance;
		}

		public object SingleOrDefault(object defaultValue)
		{
			containerService.CheckStatusIsGood(containerContext);
			if (isEnumerable)
				return containerService.GetAllValues();
			if (containerService.Instances.Length == 0)
				return defaultValue;
			containerService.CheckSingleValue(containerContext);
			return containerService.Instances[0].Instance;
		}

		public bool IsOk()
		{
			return containerService.Status == ServiceStatus.Ok;
		}

		public IEnumerable<object> All()
		{
			containerService.CheckStatusIsGood(containerContext);
			return containerService.GetAllValues();
		}

		public void DumpConstructionLog(ISimpleLogWriter writer)
		{
			containerService.WriteConstructionLog(writer,containerContext);
		}

		public string GetConstructionLog()
		{
			var logWriter = new SimpleTextLogWriter();
			DumpConstructionLog(logWriter);
			return logWriter.GetText();
		}
	}
}