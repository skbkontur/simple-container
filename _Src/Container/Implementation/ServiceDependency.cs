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

		public static ServiceDependency Constant(ParameterInfo formalParameter, object value)
		{
			return new ServiceDependency {Status = ServiceDependencyStatus.Ok, Value = value, Name = formalParameter.Name};
		}

		public static ServiceDependency Service(ContainerService service, object value)
		{
			return new ServiceDependency {Status = ServiceDependencyStatus.Ok, Value = value, ContainerService = service};
		}

		public static ServiceDependency NotResolved(ContainerService service)
		{
			return new ServiceDependency {Status = ServiceDependencyStatus.NotResolved, ContainerService = service};
		}

		public static ServiceDependency Error(string name, string message, params object[] args)
		{
			return new ServiceDependency
			{
				Status = ServiceDependencyStatus.Error,
				Name = name,
				Message = string.Format(message, args)
			};
		}

		public static ServiceDependency Error(ParameterInfo parameterInfo, string message, params object[] args)
		{
			return Error(parameterInfo.Name, message, args);
		}

		public static ServiceDependency ServiceError(ContainerService service)
		{
			return new ServiceDependency {Status = ServiceDependencyStatus.ServiceError, ContainerService = service};
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