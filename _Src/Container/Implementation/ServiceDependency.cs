using System.Reflection;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	internal class ServiceDependency
	{
		public ContainerService ContainerService { get; private set; }
		public object Value { get; private set; }
		public string Name { get; private set; }
		public string ErrorMessage { get; private set; }
		public ServiceStatus Status { get; private set; }

		private ServiceDependency()
		{
		}

		public static ServiceDependency Constant(ParameterInfo formalParameter, object value)
		{
			return new ServiceDependency {Status = ServiceStatus.Ok, Value = value, Name = formalParameter.Name};
		}

		public static ServiceDependency Service(ContainerService service, object value)
		{
			return new ServiceDependency {Status = ServiceStatus.Ok, Value = value, ContainerService = service};
		}

		public static ServiceDependency NotResolved(ContainerService service)
		{
			return new ServiceDependency {Status = ServiceStatus.NotResolved, ContainerService = service};
		}

		public static ServiceDependency Error(string name, string message, params object[] args)
		{
			return new ServiceDependency
			{
				Status = ServiceStatus.Error,
				Name = name,
				ErrorMessage = string.Format(message, args)
			};
		}

		public static ServiceDependency Error(ParameterInfo parameterInfo, string message, params object[] args)
		{
			return Error(parameterInfo.Name, message, args);
		}

		public static ServiceDependency ServiceError(ContainerService service)
		{
			return new ServiceDependency {Status = ServiceStatus.DependencyError, ContainerService = service};
		}

		public void WriteConstructionLog(ConstructionLogContext context)
		{
			context.WriteIndent();
			if (ContainerService != null)
			{
				context.UsedFromDependency = this;
				ContainerService.WriteConstructionLog(context);
				return;
			}
			if (Value != null && Value.GetType().IsSimpleType())
			{
				context.Writer.WriteName(Name);
				context.Writer.WriteMeta(" -> " + Value);
			}
			else
				context.Writer.WriteName(Name ?? Value.GetType().FormatName());
			if (Status == ServiceStatus.Error)
				context.Writer.WriteMeta(" <---------------");
			context.Writer.WriteNewLine();
		}
	}
}