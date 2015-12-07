using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	internal class ServiceDependency
	{
		public ContainerService ContainerService { get; private set; }
		public object Value { get; private set; }
		public string Name { get; private set; }
		public string Comment { get; set; }
		public ServiceStatus Status { get; private set; }

		private ServiceDependency()
		{
		}

		public ServiceDependency CastTo(Type targetType)
		{
			object castedValue;
			if (!TryCast(Value, targetType, out castedValue))
				return Error(ContainerService, Name, "can't cast value [{0}] from [{1}] to [{2}] for dependency [{3}]",
					Value, Value.GetType().FormatName(), targetType.FormatName(), Name);
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

		public static ServiceDependency Constant(ParameterInfo formalParameter, object value)
		{
			return new ServiceDependency
			{
				Status = ServiceStatus.Ok,
				Value = value,
				constantKind = ConstantKind.Value
			}.WithName(formalParameter, null);
		}

		public static ServiceDependency Resource(ParameterInfo formalParameter, string resourceName, Stream value)
		{
			return new ServiceDependency
			{
				Status = ServiceStatus.Ok,
				constantKind = ConstantKind.Resource,
				resourceName = resourceName,
				Value = value
			}.WithName(null, formalParameter.Name);
		}

		public static ServiceDependency Service(ContainerService service, object value, string name = null)
		{
			return new ServiceDependency
			{
				Status = ServiceStatus.Ok,
				Value = value,
				ContainerService = service
			}.WithName(null, name);
		}

		public static ServiceDependency NotResolved(ContainerService service, string name = null)
		{
			return new ServiceDependency
			{
				Status = ServiceStatus.NotResolved,
				ContainerService = service
			}.WithName(null, name);
		}

		public static ServiceDependency Error(ContainerService containerService, string name, string message,
			params object[] args)
		{
			return new ServiceDependency
			{
				ContainerService = containerService,
				Status = ServiceStatus.Error,
				Comment = string.Format(message, args)
			}.WithName(null, name);
		}

		public static ServiceDependency ServiceError(ContainerService service, string name = null)
		{
			return new ServiceDependency
			{
				Status = ServiceStatus.DependencyError,
				ContainerService = service
			}.WithName(null, name);
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
					WriteValue(context, Value, true);
				if (constantKind == ConstantKind.Resource)
					context.Writer.WriteMeta(string.Format(" resource [{0}]", resourceName));
			}
			if (Status == ServiceStatus.Error)
				context.Writer.WriteMeta(" <---------------");
			context.Writer.WriteNewLine();
		}

		private static void WriteValue(ConstructionLogContext context, object value, bool isTop)
		{
			var formattedValue = FormatAsSimpleType(value, context);
			if (formattedValue != null)
			{
				if (isTop && value != null && context.ValueFormatters.ContainsKey(value.GetType()))
					context.Writer.WriteMeta(" const");
				context.Writer.WriteMeta(" -> " + formattedValue);
				return;
			}
			if (isTop)
				context.Writer.WriteMeta(" const");
			context.Indent++;
			var enumerable = value as IEnumerable;
			if (enumerable == null)
			{
				var properties = value.GetType()
					.GetProperties(BindingFlags.Public | BindingFlags.Instance)
					.Where(x => x.CanRead && (x.CanWrite || IsAutoProperty(x)))
					.ToArray();
				if (properties.Length > 0)
					WriteMembers(context, properties, value);
				else
					WriteMembers(context, value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance), value);
			}
			else
				foreach (var item in enumerable)
				{
					context.Writer.WriteNewLine();
					context.WriteIndent();
					formattedValue = FormatAsSimpleType(item, context);
					if (formattedValue == null)
					{
						context.Writer.WriteName("item");
						WriteValue(context, item, false);
					}
					else
						context.Writer.WriteMeta(formattedValue);
				}
			context.Indent--;
		}

		private static bool IsAutoProperty(PropertyInfo propertyInfo)
		{
			var getMethod = propertyInfo.GetGetMethod(true);
			var setMethod = propertyInfo.GetSetMethod(true);
			return getMethod != null &&
				   setMethod != null &&
				   getMethod.IsDefined<CompilerGeneratedAttribute>() &&
				   setMethod.IsDefined<CompilerGeneratedAttribute>();
		}

		private static void WriteMembers(ConstructionLogContext context, IEnumerable<MemberInfo> members, object value)
		{
			foreach (var m in members)
			{
				var propVal = m is FieldInfo ? ((FieldInfo) m).GetValue(value) : ((PropertyInfo) m).GetValue(value);
				context.Writer.WriteNewLine();
				context.WriteIndent();
				context.Writer.WriteName(m.Name);
				WriteValue(context, propVal, false);
			}
		}

		private static string FormatAsSimpleType(object value, ConstructionLogContext context)
		{
			Func<object, string> formatter;
			return value == null || value.GetType().IsSimpleType()
				? InternalHelpers.DumpValue(value)
				: context.ValueFormatters.TryGetValue(value.GetType(), out formatter)
					? formatter(value)
					: null;
		}

		private ServiceDependency WithName(ParameterInfo parameter, string name)
		{
			Name = BuildName(parameter, name);
			return this;
		}

		private string BuildName(ParameterInfo parameter, string name)
		{
			if (name != null)
				return name;
			var type = GetDependencyType();
			return type == null || type.IsSimpleType() || type.UnwrapEnumerable() != type ? parameter.Name : type.FormatName();
		}

		private Type GetDependencyType()
		{
			if (ContainerService != null)
				return ContainerService.Type;
			if (Value != null)
				return Value.GetType();
			return null;
		}

		private ConstantKind? constantKind;
		private string resourceName;

		private enum ConstantKind
		{
			Value,
			Resource
		}
	}
}