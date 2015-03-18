using System;
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
		private bool isConstant;

		private ServiceDependency()
		{
		}

		public ServiceDependency CastTo(Type targetType)
		{
			if (Value == null || targetType.IsInstanceOfType(Value))
				return this;
			var underlyingType = Nullable.GetUnderlyingType(targetType);
			if (underlyingType != null)
				targetType = underlyingType;
			if (Value is int && targetType == typeof (long))
				return CloneWithValue((long) (int) Value);
			return Error(ContainerService, Name, "can't cast value [{0}] from [{1}] to [{2}] for dependency [{3}]",
				Value, Value.GetType().FormatName(), targetType.FormatName(), Name);
		}

		private ServiceDependency CloneWithValue(object value)
		{
			return new ServiceDependency
			{
				ContainerService = ContainerService,
				ErrorMessage = ErrorMessage,
				Name = Name,
				Status = Status,
				isConstant = isConstant,
				Value = value
			};
		}

		public static ServiceDependency Constant(ParameterInfo formalParameter, object value)
		{
			return new ServiceDependency
			{
				Status = ServiceStatus.Ok,
				Value = value,
				Name = formalParameter.Name,
				isConstant = true
			};
		}

		public static ServiceDependency Service(ContainerService service, object value)
		{
			return new ServiceDependency
			{
				Status = ServiceStatus.Ok,
				Value = value,
				ContainerService = service,
				Name = service.Type.FormatName()
			};
		}

		public static ServiceDependency NotResolved(ContainerService service)
		{
			return new ServiceDependency
			{
				Status = ServiceStatus.NotResolved,
				ContainerService = service,
				Name = service.Type.FormatName()
			};
		}

		public static ServiceDependency Error(ContainerService containerService, string name, string message, params object[] args)
		{
			return new ServiceDependency
			{
				ContainerService = containerService,
				Status = ServiceStatus.Error,
				Name = name,
				ErrorMessage = string.Format(message, args)
			};
		}

		public static ServiceDependency Error(ContainerService containerService, ParameterInfo parameterInfo, string message, params object[] args)
		{
			return Error(containerService, parameterInfo.Name, message, args);
		}

		public static ServiceDependency ServiceError(ContainerService service)
		{
			return new ServiceDependency
			{
				Status = ServiceStatus.DependencyError,
				ContainerService = service,
				Name = service.Type.Name
			};
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
			context.Writer.WriteName(Name);
			if (Status == ServiceStatus.Ok && isConstant)
				context.Writer.WriteMeta(" -> " + (Value ?? "<null>"));
			if (Status == ServiceStatus.Error)
				context.Writer.WriteMeta("! <---------------");
			context.Writer.WriteNewLine();
		}
	}
}