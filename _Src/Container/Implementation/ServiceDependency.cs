using System;
using System.Reflection;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	internal class ServiceDependency
	{
		public ContainerService ContainerService { get; private set; }
		public object Value { get; private set; }

		public string Name
		{
			get { return name ?? ContainerService.Type.FormatName(); }
		}

		public string ErrorMessage { get; private set; }
		public ServiceStatus Status { get; private set; }

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
				name = Name,
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
				name = formalParameter.Name,
				isConstant = true
			};
		}

		public static ServiceDependency Service(ContainerService service, object value, string name = null)
		{
			return new ServiceDependency
			{
				Status = ServiceStatus.Ok,
				Value = value,
				ContainerService = service,
				name = name
			};
		}

		public static ServiceDependency NotResolved(ContainerService service, string name = null)
		{
			return new ServiceDependency
			{
				Status = ServiceStatus.NotResolved,
				ContainerService = service,
				name = name
			};
		}

		public static ServiceDependency Error(ContainerService containerService, string name, string message,
			params object[] args)
		{
			return new ServiceDependency
			{
				ContainerService = containerService,
				Status = ServiceStatus.Error,
				name = name,
				ErrorMessage = string.Format(message, args)
			};
		}

		public static ServiceDependency Error(ContainerService containerService, ParameterInfo parameterInfo, string message,
			params object[] args)
		{
			return Error(containerService, parameterInfo.Name, message, args);
		}

		public static ServiceDependency ServiceError(ContainerService service, string name = null)
		{
			return new ServiceDependency
			{
				Status = ServiceStatus.DependencyError,
				ContainerService = service,
				name = name
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
			if (Status == ServiceStatus.Ok && isConstant && (Value == null || Value.GetType().IsSimpleType()))
				context.Writer.WriteMeta(" -> " + DumpValue(Value));
			if (Status != ServiceStatus.Ok)
				context.Writer.WriteMeta("!");
			if (Status == ServiceStatus.Error)
				context.Writer.WriteMeta(" <---------------");
			context.Writer.WriteNewLine();
		}

		private static string DumpValue(object value)
		{
			if (value == null)
				return "<null>";
			var result = value.ToString();
			return value is bool ? result.ToLower() : result;
		}

		private bool isConstant;
		private string name;
	}
}