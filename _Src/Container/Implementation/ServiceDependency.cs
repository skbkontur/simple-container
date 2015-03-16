using System.Collections.Generic;
using System.Reflection;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class ServiceDependency
	{
		public ContainerService service;
		public object Value { get; private set; }
		public bool IsEnumerable { get; private set; }
		public string Name { get; private set; }
		public string Message { get; private set; }
		public ServiceStatus Status { get; private set; }
		public bool ValueAssigned { get; private set; }

		public static ServiceDependency Constant(ParameterInfo formalParameter, object instance)
		{
			return new ServiceDependency
			{
				Name = formalParameter.Name,
				Value = instance,
				ValueAssigned = true
			};
		}

		public static ServiceDependency Failed(ParameterInfo formalParameter, string message, params object[] args)
		{
			return new ServiceDependency
			{
				Name = formalParameter.Name,
				Message = string.Format(message, args),
				Status = ServiceStatus.Failed
			};
		}

		public static ServiceDependency Service(ContainerService service)
		{
			return new ServiceDependency {service = service};
		}

		public static ServiceDependency Enumerable(ContainerService service)
		{
			return new ServiceDependency {service = service, IsEnumerable = true};
		}

		public static ServiceDependency ServiceWithDefaultValue(ContainerService service, object defaultValue)
		{
			return new ServiceDependency {service = service, Value = defaultValue, ValueAssigned = true};
		}

		public void Format(ISimpleLogWriter writer, int indent, int declaredContractsCount, ISet<CacheKey> seen)
		{
			writer.WriteIndent(indent);
			if (service != null)
				service.Format(writer, indent, declaredContractsCount, seen, this);
			else
				FormatConstant(writer);
		}

		private void FormatConstant(ISimpleLogWriter writer)
		{
			if (Value == null || Value.GetType().IsSimpleType())
			{
				writer.WriteName(Name);
				writer.WriteMeta(" -> " + (Value ?? "<null>"));
			}
			else
				writer.WriteName(Value.GetType().FormatName());
			writer.WriteNewLine();
		}
	}
}