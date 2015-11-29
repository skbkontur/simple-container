using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class ContainerService
	{
		private readonly object lockObject = new object();
		private string comment;
		private Exception constructionException;
		private ServiceDependency[] dependencies;
		internal volatile bool disposing;
		private volatile bool initialized;
		private volatile bool initializing;
		private InstanceWrap[] instances;

		private object[] typedArray;

		private ContainerService()
		{
		}

		public ServiceStatus Status { get; private set; }
		public Type Type { get; private set; }
		public string[] UsedContracts { get; private set; }

		public ServiceName Name
		{
			get { return new ServiceName(Type, UsedContracts); }
		}

		public object GetSingleValue(ContainerContext containerContext, bool hasDefaultValue, object defaultValue)
		{
			CheckStatusIsGood(containerContext);
			if (instances.Length == 1)
				return instances[0].Instance;
			if (instances.Length == 0 && hasDefaultValue)
				return defaultValue;
			string message;
			if (instances.Length == 0)
			{
				var targetType = typeof (Delegate).IsAssignableFrom(Type) ? Type.DeclaringType : Type;
				message = string.Format("no instances for [{0}]", targetType.FormatName());
				var notResolvedRoot = SearchForNotResolvedRoot();
				if (notResolvedRoot != this)
					message += string.Format(" because [{0}] has no instances", notResolvedRoot.Type.FormatName());
			}
			else
				message = FormatManyImplementationsMessage();
			var assemblies = containerContext.typesList.GetAssemblies()
				.OrderBy(x => x.GetName().Name)
				.Select(x => "\t" + x.GetName().Name).JoinStrings("\r\n");
			throw new SimpleContainerException(string.Format("{0}\r\n\r\n{1}\r\nscanned assemblies\r\n{2}",
				message, GetConstructionLog(containerContext), assemblies));
		}

		public ServiceDependency AsImplicitDependency(ContainerContext containerContext, bool isEnumerable)
		{
			return AsDependency(containerContext, "() => " + Type.FormatName(), isEnumerable);
		}

		public ServiceDependency AsDependency(ContainerContext containerContext, string dependencyName, bool isEnumerable)
		{
			if (Status.IsBad())
				return ServiceDependency.ServiceError(this, dependencyName);
			if (Status == ServiceStatus.NotResolved)
				return ServiceDependency.NotResolved(this, dependencyName);
			if (!isEnumerable)
				return instances.Length > 1
					? ServiceDependency.Error(this, dependencyName, FormatManyImplementationsMessage())
					: ServiceDependency.Service(this, instances[0].Instance, dependencyName);
			return ServiceDependency.Service(this, GetAllValues(containerContext), dependencyName);
		}

		public IEnumerable<object> GetAllValues(ContainerContext containerContext)
		{
			CheckStatusIsGood(containerContext);
			return typedArray ?? (typedArray = instances.Select(x => x.Instance).CastToObjectArrayOf(Type));
		}

		public void EnsureInitialized(ContainerContext containerContext, ContainerService root)
		{
			if (Status != ServiceStatus.Ok)
				return;
			if (!initialized)
				lock (lockObject)
					if (!initialized)
					{
						initializing = true;
						try
						{
							if (dependencies != null)
								foreach (var dependency in dependencies)
									if (dependency.ContainerService != null)
										dependency.ContainerService.EnsureInitialized(containerContext, root);
							foreach (var instance in instances)
								instance.EnsureInitialized(this, containerContext, root);
							initialized = true;
						}
						finally
						{
							initializing = false;
						}
					}
		}

		public void CollectInstances(Type interfaceType, ISet<object> seen, List<ServiceInstance> target)
		{
			if (Status.IsBad())
				return;
			if (dependencies != null)
				foreach (var dependency in dependencies)
					if (dependency.ContainerService != null)
						dependency.ContainerService.CollectInstances(interfaceType, seen, target);
			foreach (var instance in instances)
			{
				var obj = instance.Instance;
				var acceptInstance = instance.Owned &&
				                     interfaceType.IsInstanceOfType(obj) &&
				                     seen.Add(obj);
				if (acceptInstance)
					target.Add(new ServiceInstance(obj, this));
			}
		}

		public string GetConstructionLog(ContainerContext containerContext)
		{
			var writer = new SimpleTextLogWriter();
			WriteConstructionLog(writer, containerContext);
			return writer.GetText();
		}

		public void WriteConstructionLog(ISimpleLogWriter writer, ContainerContext containerContext)
		{
			WriteConstructionLog(new ConstructionLogContext(writer, containerContext.valueFormatters));
		}

		public void WriteConstructionLog(ConstructionLogContext context)
		{
			var usedContracts = UsedContracts;
			var attentionRequired = Status.IsBad() ||
			                        Status == ServiceStatus.NotResolved && (context.UsedFromDependency == null ||
			                                                                context.UsedFromDependency.Status ==
			                                                                ServiceStatus.NotResolved);
			if (attentionRequired)
				context.Writer.WriteMeta("!");
			var formattedName = context.UsedFromDependency == null ? Type.FormatName() : context.UsedFromDependency.Name;
			context.Writer.WriteName(formattedName);
			if (usedContracts != null && usedContracts.Length > 0)
				context.Writer.WriteUsedContract(InternalHelpers.FormatContractsKey(usedContracts));
			if (!context.Seen.Add(new ServiceName(Type, usedContracts)))
			{
				context.Writer.WriteNewLine();
				return;
			}
			if (instances.Length > 1)
				context.Writer.WriteMeta("++");
			if (Status == ServiceStatus.Error)
				context.Writer.WriteMeta(" <---------------");
			else
			{
				var logComment = comment;
				if (logComment == null && context.UsedFromDependency != null &&
				    context.UsedFromDependency.Status != ServiceStatus.Error)
					logComment = context.UsedFromDependency.Comment;
				if (logComment != null)
				{
					context.Writer.WriteMeta(" - ");
					context.Writer.WriteMeta(logComment);
				}
			}
			if (context.UsedFromDependency != null && context.UsedFromDependency.Status == ServiceStatus.Ok &&
			    (context.UsedFromDependency.Value == null || context.UsedFromDependency.Value.GetType().IsSimpleType()))
				context.Writer.WriteMeta(" -> " + InternalHelpers.DumpValue(context.UsedFromDependency.Value));
			if (initializing)
				context.Writer.WriteMeta(", initializing ...");
			else if (disposing)
				context.Writer.WriteMeta(", disposing ...");
			context.Writer.WriteNewLine();
			if (dependencies != null)
				foreach (var d in dependencies)
				{
					context.Indent++;
					d.WriteConstructionLog(context);
					context.Indent--;
				}
		}

		private void CheckStatusIsGood(ContainerContext containerContext)
		{
			if (Status.IsGood())
				return;
			var errorTarget = SearchForError();
			if (errorTarget == null)
				throw new InvalidOperationException("assertion failure: can't find error target");
			var constructionLog = GetConstructionLog(containerContext);
			var currentConstructionException = errorTarget.service != null ? errorTarget.service.constructionException : null;
			var message = currentConstructionException != null
				? string.Format("service [{0}] construction exception", errorTarget.service.Type.FormatName())
				: (errorTarget.service != null ? errorTarget.service.comment : errorTarget.dependency.Comment);
			throw new SimpleContainerException(message + "\r\n\r\n" + constructionLog, currentConstructionException);
		}

		private ErrorTarget SearchForError()
		{
			if (Status == ServiceStatus.Error)
				return new ErrorTarget {service = this};
			if (dependencies != null)
				foreach (var dependency in dependencies)
				{
					if (dependency.Status == ServiceStatus.Error)
						return new ErrorTarget {dependency = dependency};
					if (dependency.ContainerService != null)
					{
						var result = dependency.ContainerService.SearchForError();
						if (result != null)
							return result;
					}
				}
			return null;
		}

		private ContainerService SearchForNotResolvedRoot()
		{
			var current = this;
			while (current.dependencies != null)
			{
				var notResolvedDependencies = current.dependencies.Where(x => x.Status == ServiceStatus.NotResolved).ToArray();
				if (notResolvedDependencies.Length != 1)
					break;
				var next = notResolvedDependencies[0].ContainerService;
				if (next == null)
					break;
				current = next;
			}
			return current;
		}

		private ServiceDependency GetLinkedDependency(ContainerContext containerContext)
		{
			if (Status == ServiceStatus.Ok)
				return ServiceDependency.Service(this, GetAllValues(containerContext));
			if (Status == ServiceStatus.NotResolved)
				return ServiceDependency.NotResolved(this);
			return ServiceDependency.ServiceError(this);
		}

		private string FormatManyImplementationsMessage()
		{
			return string.Format("many instances for [{0}]\r\n{1}", Type.FormatName(),
				instances.Select(x => "\t" + x.Instance.GetType().FormatName()).JoinStrings("\r\n"));
		}

		private class ErrorTarget
		{
			public ServiceDependency dependency;
			public ContainerService service;
		}

		public static ContainerService Error(ServiceName name, string message)
		{
			var result = new Builder(name);
			result.SetError(message);
			return result.GetService();
		}

		public class Builder
		{
			[ThreadStatic]
			private static Builder current;
			private readonly List<InstanceWrap> instances = new List<InstanceWrap>();
			private List<ServiceDependency> dependencies;
			private bool built;
			private ContainerService target;
			private List<string> usedContractNames;

			public Builder(ServiceName name)
			{
				Name = name;
				target = new ContainerService {Type = name.Type};
			}

			public ServiceConfiguration Configuration { get; private set; }
			public ResolutionContext Context { get; set; }
			public string[] DeclaredContracts { get; set; }
			public IObjectAccessor Arguments { get; set; }
			public bool CreateNew { get; set; }
			public ServiceName Name { get; private set; }

			public Type Type
			{
				get { return target.Type; }
			}

			public ServiceStatus Status
			{
				get { return target.Status; }
			}

			public string[] FinalUsedContracts
			{
				get { return target.UsedContracts; }
			}

			public void SetConfiguration(ServiceConfiguration newConfiguration)
			{
				Configuration = newConfiguration;
				SetComment(Configuration.Comment);
				foreach (var contract in Configuration.Contracts)
				{
					if (usedContractNames == null)
						usedContractNames = new List<string>();
					if (!usedContractNames.Contains(contract, StringComparer.OrdinalIgnoreCase))
						usedContractNames.Add(contract);
				}
			}

			public static Builder Current
			{
				get { return current; }
			}

			public ServiceName GetFinalName()
			{
				return new ServiceName(Type, FinalUsedContracts);
			}

			public ServiceName GetDeclaredName()
			{
				return new ServiceName(Type, DeclaredContracts);
			}

			public void AddInstance(object instance, bool owned)
			{
				instances.Add(new InstanceWrap(instance, owned));
			}

			public void AddDependency(ServiceDependency dependency, bool isUnion)
			{
				if (dependencies == null)
					dependencies = new List<ServiceDependency>();
				dependencies.Add(dependency);
				target.Status = DependencyStatusToServiceStatus(dependency.Status, isUnion);
			}

			public void SetComment(string value)
			{
				target.comment = value;
			}

			public void LinkTo(ContainerContext containerContext, ContainerService childService, string comment)
			{
				var dependency = childService.GetLinkedDependency(containerContext);
				dependency.Comment = comment;
				AddDependency(dependency, true);
				UnionUsedContracts(childService);
				if (target.Status.IsGood())
					foreach (var instance in childService.instances)
						if (!instances.Contains(instance))
							instances.Add(instance);
			}

			public int FilterInstances(Func<object, bool> filter)
			{
				return instances.RemoveAll(o => !filter(o.Instance));
			}

			public void UnionUsedContracts(ContainerService dependency)
			{
				if (dependency.UsedContracts == null)
					return;
				if (usedContractNames == null)
					usedContractNames = new List<string>();
				var contractsToAdd = dependency.UsedContracts
					.Where(x => !usedContractNames.Contains(x, StringComparer.OrdinalIgnoreCase))
					.Where(x => DeclaredContracts.Any(x.EqualsIgnoringCase));
				foreach (var n in contractsToAdd)
					usedContractNames.Add(n);
			}

			public void SetError(string newErrorMessage)
			{
				target.comment = newErrorMessage;
				target.Status = ServiceStatus.Error;
			}

			public void SetError(Exception error)
			{
				target.constructionException = error;
				target.Status = ServiceStatus.Error;
			}

			public void EndResolveDependencies()
			{
				if (target.UsedContracts == null)
					target.UsedContracts = usedContractNames == null || DeclaredContracts == null
						? InternalHelpers.emptyStrings
						: DeclaredContracts
							.Where(x => usedContractNames.Contains(x, StringComparer.OrdinalIgnoreCase))
							.ToArray();
			}

			public void Reuse(ContainerService containerService)
			{
				target = containerService;
				built = true;
			}

			public ContainerService GetService()
			{
				if (built)
					return target;
				EndResolveDependencies();
				if (target.Status == ServiceStatus.Ok && instances.Count == 0)
					target.Status = ServiceStatus.NotResolved;
				if (Status == ServiceStatus.Ok && Arguments != null)
				{
					var unused = Arguments.GetUnused().ToArray();
					if (unused.Any())
						SetError(string.Format("arguments [{0}] are not used", unused.JoinStrings(",")));
				}
				target.instances = instances.ToArray();
				if (dependencies != null)
					target.dependencies = dependencies.ToArray();
				built = true;
				return target;
			}

			private static ServiceStatus DependencyStatusToServiceStatus(ServiceStatus dependencyStatus, bool isUnion)
			{
				if (dependencyStatus == ServiceStatus.Error)
					return ServiceStatus.DependencyError;
				if (dependencyStatus == ServiceStatus.NotResolved && isUnion)
					return ServiceStatus.Ok;
				return dependencyStatus;
			}

			public bool DontUse()
			{
				return Configuration.DontUseIt || Type.IsDefined("DontUseAttribute");
			}

			public void CreateInstanceBy(Func<object> creator, bool owned)
			{
				object instance;
				var prev = current;
				try
				{
					current = this;
					instance = creator();
				}
				catch (ServiceCouldNotBeCreatedException e)
				{
					if (!string.IsNullOrEmpty(e.Message))
						SetComment(e.Message);
					return;
				}
				catch (SimpleContainerException)
				{
					return;
				}
				catch (Exception e)
				{
					SetError(e);
					return;
				}
				finally
				{
					current = prev;
				}
				AddInstance(instance, owned);
			}

			public void CreateInstance(MethodBase method, object self, object[] actualArguments)
			{
				CreateInstanceBy(() => method.Compile()(self, actualArguments), true);
			}
		}
	}
}