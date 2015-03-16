using System.Reflection;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	internal class ServiceDependency
	{
		public ContainerService ContainerService { get; private set; }
		public object Value { get; private set; }
		public string Name { get; private set; }
		public string Message { get; private set; }
		public ServiceDependencyStatus Status { get; private set; }

		private ServiceDependency()
		{
		}

		public static ServiceDependency Constant(ParameterInfo formalParameter, object instance)
		{
			return new ServiceDependency
			{
				Name = formalParameter.Name,
				Value = instance
			};
		}

		public static ServiceDependency Failed(string name, string message, params object[] args)
		{
			return new ServiceDependency
			{
				Name = name,
				Message = string.Format(message, args),
				Status = ServiceDependencyStatus.Failed
			};
		}

		public static ServiceDependency NotResolved(ContainerService containerService)
		{
			return new ServiceDependency {ContainerService = containerService, Status = ServiceDependencyStatus.ServiceNotResolved};
		}

		public static ServiceDependency Service(ContainerService service, object value)
		{
			return new ServiceDependency {ContainerService = service, Value = value};
		}

		public static ServiceDependency FailedService(ContainerService service)
		{
			return new ServiceDependency {ContainerService = service, Status = ServiceDependencyStatus.ServiceFailed};
		}

		public void WriteConstructionLog(ConstructionLogContext context)
		{
			context.WriteIndent();
			if (ContainerService != null)
			{
				context.Indent++;
				context.UsedFromDependency = this;
				ContainerService.WriteConstructionLog(context);
				context.Indent--;
				return;
			}
			if (Value == null || Value.GetType().IsSimpleType())
			{
				context.Writer.WriteName(Name);
				context.Writer.WriteMeta(" -> " + (Value ?? "<null>"));
			}
			else
				context.Writer.WriteName(Name ?? Value.GetType().FormatName());
			context.Writer.WriteNewLine();
		}
	}
}