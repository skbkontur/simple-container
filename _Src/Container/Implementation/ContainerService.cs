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
		public ServiceStatus Status { get; private set; }
		public Type Type { get; private set; }
		public string[] UsedContracts { get; private set; }

		public ServiceName Name
		{
			get { return new ServiceName(Type, UsedContracts); }
		}

		private readonly object lockObject = new object();

		private object[] typedArray;
		private volatile bool initialized;
		private volatile bool initializing;
		internal volatile bool disposing;
		private string comment;
		private Exception constructionException;
		private ServiceDependency[] dependencies;
		private InstanceWrap[] instances;
		private SimpleContainer container;

		private ContainerService()
		{
		}

		public object GetSingleValue(bool hasDefaultValue, object defaultValue)
		{
			CheckStatusIsGood();
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
			throw new SimpleContainerException(string.Format("{0}\r\n\r\n{1}", message, GetConstructionLog()));
		}

		public ServiceDependency AsSingleInstanceDependency(string dependencyName)
		{
			if (Status.IsBad())
				return ServiceDependency.ServiceError(this, dependencyName);
			if (Status == ServiceStatus.NotResolved)
				return ServiceDependency.NotResolved(this, dependencyName);
			return instances.Length > 1
				? ServiceDependency.Error(this, dependencyName, FormatManyImplementationsMessage())
				: ServiceDependency.Service(this, instances[0].Instance, dependencyName);
		}

		public IEnumerable<object> GetAllValues()
		{
			CheckStatusIsGood();
			return typedArray ?? (typedArray = instances.Select(x => x.Instance).CastToObjectArrayOf(Type));
		}

		public void EnsureInitialized(LogInfo infoLogger)
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
										dependency.ContainerService.EnsureInitialized(infoLogger);
							foreach (var instance in instances)
								instance.EnsureInitialized(this, infoLogger);
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

		public string GetConstructionLog()
		{
			var writer = new SimpleTextLogWriter();
			WriteConstructionLog(writer);
			return writer.GetText();
		}

		public void WriteConstructionLog(ISimpleLogWriter writer)
		{
			WriteConstructionLog(new ConstructionLogContext(writer, container.valueFormatters));
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
				//todo refactor this shit
				if (Status == ServiceStatus.Error && comment.Contains("cyclic dependency"))
					context.Writer.WriteMeta(" <---------------");
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

		private void CheckStatusIsGood()
		{
			if (Status.IsGood())
				return;
			var errorTarget = SearchForError();
			if (errorTarget == null)
				throw new InvalidOperationException("assertion failure: can't find error target");
			var constructionLog = GetConstructionLog();
			var currentConstructionException = errorTarget.service != null ? errorTarget.service.constructionException : null;
			var message = currentConstructionException != null
				? string.Format("service [{0}] construction exception", errorTarget.service.Type.FormatName())
				: (errorTarget.service != null ? errorTarget.service.comment : errorTarget.dependency.Comment);
			throw new SimpleContainerException(message + "\r\n\r\n" + constructionLog, currentConstructionException);
		}

		private class ErrorTarget
		{
			public ContainerService service;
			public ServiceDependency dependency;
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

		private ServiceDependency GetLinkedDependency()
		{
			if (Status == ServiceStatus.Ok)
				return ServiceDependency.Service(this, GetAllValues());
			if (Status == ServiceStatus.NotResolved)
				return ServiceDependency.NotResolved(this);
			return ServiceDependency.ServiceError(this);
		}

		private string FormatManyImplementationsMessage()
		{
			return string.Format("many instances for [{0}]\r\n{1}", Type.FormatName(),
				instances.Select(x => "\t" + x.Instance.GetType().FormatName()).JoinStrings("\r\n"));
		}

		public class Builder
		{
			public ServiceConfiguration Configuration { get; private set; }
			private List<ServiceDependency> dependencies;
			public IObjectAccessor Arguments { get; private set; }
			public bool CreateNew { get; private set; }
			private readonly List<InstanceWrap> instances = new List<InstanceWrap>();
			private List<string> usedContractNames;
			private ContainerService target;
			private bool reused;
			public ResolutionContext Context { get; private set; }

			public Builder(Type type, ResolutionContext context, bool createNew, IObjectAccessor arguments)
			{
				Arguments = arguments;
				CreateNew = createNew;
				target = new ContainerService {Type = type};
				Context = context;
				DeclaredContracts = context.Contracts.ToArray();
				try
				{
					Configuration = context.Container.GetConfiguration(Type, context);
				}
				catch (Exception e)
				{
					SetError(e);
					return;
				}
				SetComment(Configuration.Comment);
				foreach (var contract in Configuration.Contracts)
				{
					if (usedContractNames == null)
						usedContractNames = new List<string>();
					if (!usedContractNames.Contains(contract, StringComparer.OrdinalIgnoreCase))
						usedContractNames.Add(contract);
				}
			}

			public bool HasNoConfiguration()
			{
				return Configuration == ServiceConfiguration.empty;
			}

			public Type Type
			{
				get { return target.Type; }
			}

			public ServiceStatus Status
			{
				get { return target.Status; }
			}

			public string[] DeclaredContracts { get; private set; }

			public string[] FinalUsedContracts
			{
				get { return target.UsedContracts; }
			}

			public ServiceName GetName()
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

			public void LinkTo(ContainerService childService, string comment)
			{
				var dependency = childService.GetLinkedDependency();
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

			public void UseAllDeclaredContracts()
			{
				target.UsedContracts = DeclaredContracts;
				usedContractNames = target.UsedContracts.ToList();
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

			public void UnionStatusFrom(ContainerService containerService)
			{
				target.Status = containerService.Status;
				target.comment = containerService.comment;
				target.constructionException = containerService.constructionException;
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
					target.UsedContracts = usedContractNames == null
						? InternalHelpers.emptyStrings
						: DeclaredContracts
							.Where(x => usedContractNames.Contains(x, StringComparer.OrdinalIgnoreCase))
							.ToArray();
			}

			public void Reuse(ContainerService containerService)
			{
				target = containerService;
				reused = true;
			}

			public ContainerService Build()
			{
				if (reused)
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
				target.container = Context.Container;
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

			public bool IgnoredImplementation()
			{
				return Configuration.IgnoredImplementation || Type.IsDefined("IgnoredImplementationAttribute");
			}

			public bool DontUse()
			{
				return Configuration.DontUseIt || Type.IsDefined("DontUseAttribute");
			}

			[ThreadStatic] private static Builder current;

			public static Builder Current
			{
				get { return current; }
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