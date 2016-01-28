using System;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	internal class ServiceDependency
	{
		public ContainerService ContainerService { get; set; }
		public object Value { get; set; }
		public string Name { get; set; }
		public string Comment { get; set; }
		public ServiceStatus Status { get; set; }
		public ConstantKind? constantKind;
		public string resourceName;

		public ServiceDependency CastTo(Type targetType)
		{
			object castedValue;
			if (!TryCast(Value, targetType, out castedValue))
				return new ServiceDependency
				{
					Comment = string.Format("can't cast value [{0}] from [{1}] to [{2}] for dependency [{3}]",
						Value, Value.GetType().FormatName(), targetType.FormatName(), Name),
					ContainerService = ContainerService,
					Name = Name,
					Status = ServiceStatus.Error
				};
			return ReferenceEquals(castedValue, Value) ? this : CloneWithValue(castedValue);
		}

		private static bool TryCast(object source, Type targetType, out object value)
		{
			if (source == null || targetType.IsInstanceOfType(source))
			{
				value = source;
				return true;
			}
			var underlyingType = Nullable.GetUnderlyingType(targetType);
			if (underlyingType != null)
				targetType = underlyingType;
			if (source is int && targetType == typeof (long))
			{
				value = (long) (int) source;
				return true;
			}
			value = null;
			return false;
		}

		private ServiceDependency CloneWithValue(object value)
		{
			return new ServiceDependency
			{
				ContainerService = ContainerService,
				Comment = Comment,
				Name = Name,
				Status = Status,
				resourceName = resourceName,
				constantKind = constantKind,
				Value = value
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
			if (Status != ServiceStatus.Ok)
				context.Writer.WriteMeta("!");
			context.Writer.WriteName(Name);
			if (Comment != null && Status != ServiceStatus.Error)
			{
				context.Writer.WriteMeta(" - ");
				context.Writer.WriteMeta(Comment);
			}
			if (Status == ServiceStatus.Ok && constantKind.HasValue)
			{
				if (constantKind == ConstantKind.Value)
					ValueFormatter.WriteValue(context, Value, true);
				if (constantKind == ConstantKind.Resource)
					context.Writer.WriteMeta(string.Format(" resource [{0}]", resourceName));
			}
			if (Status == ServiceStatus.Error)
				context.Writer.WriteMeta(" <---------------");
			context.Writer.WriteNewLine();
		}

		public enum ConstantKind
		{
			Value,
			Resource
		}
	}
}