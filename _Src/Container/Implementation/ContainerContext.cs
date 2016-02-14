using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class ContainerContext
	{
		public Dictionary<Type, Func<object, string>> valueFormatters;
		public TypesList typesList;
		public LogInfo infoLogger;
		public GenericsAutoCloser genericsAutoCloser;

		public Type[] AllTypes()
		{
			return allTypes ??
			       (allTypes = typesList.Types.Where(x => x.Assembly != typeof (SimpleContainer).Assembly).ToArray());
		}

		private Type[] allTypes;


		public ServiceDependency Constant(ParameterInfo formalParameter, object value)
		{
			return SetName(new ServiceDependency
			{
				Status = ServiceStatus.Ok,
				Value = value,
				constantKind = ServiceDependency.ConstantKind.Value
			}, formalParameter, null);
		}

		public ServiceDependency Resource(ParameterInfo formalParameter, string resourceName, Stream value)
		{
			return SetName(new ServiceDependency
			{
				Status = ServiceStatus.Ok,
				constantKind = ServiceDependency.ConstantKind.Resource,
				resourceName = resourceName,
				Value = value
			}, null, formalParameter.Name);
		}

		public ServiceDependency Service(ContainerService service, object value, string name = null)
		{
			return SetName(new ServiceDependency
			{
				Status = ServiceStatus.Ok,
				Value = value,
				ContainerService = service
			}, null, name);
		}

		public ServiceDependency NotResolved(ContainerService service, string name = null)
		{
			return SetName(new ServiceDependency
			{
				Status = ServiceStatus.NotResolved,
				ContainerService = service
			}, null, name);
		}

		public ServiceDependency Error(ContainerService containerService, string name, string message,
			params object[] args)
		{
			return SetName(new ServiceDependency
			{
				ContainerService = containerService,
				Status = ServiceStatus.Error,
				Comment = string.Format(message, args)
			}, null, name);
		}

		public ServiceDependency ServiceError(ContainerService service, string name = null)
		{
			return SetName(new ServiceDependency
			{
				Status = ServiceStatus.DependencyError,
				ContainerService = service
			}, null, name);
		}

		private ServiceDependency SetName(ServiceDependency serviceDependency, ParameterInfo parameter, string name)
		{
			if (name != null)
				serviceDependency.Name = name;
			else
			{
				Type type = null;
				if (serviceDependency.ContainerService != null)
					type = serviceDependency.ContainerService.Type;
				else if (serviceDependency.Value != null)
					type = serviceDependency.Value.GetType();
				serviceDependency.Name = type == null || type.IsSimpleType() || type.UnwrapEnumerable() != type ||
				                         valueFormatters.ContainsKey(type)
					? parameter.Name
					: type.FormatName();
			}
			return serviceDependency;
		}
	}
}