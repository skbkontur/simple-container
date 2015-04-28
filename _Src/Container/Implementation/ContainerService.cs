using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation.Hacks;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class ContainerService
	{
		public ServiceStatus status;
		private List<string> usedContractNames;
		private readonly List<InstanceWrap> instances = new List<InstanceWrap>();
		private IEnumerable<object> typedArray;
		private readonly object lockObject = new object();
		private bool instantiated;
		private volatile bool runCalled;
		public Type Type { get; private set; }
		public string[] FinalUsedContracts { get; private set; }
		public IObjectAccessor Arguments { get; private set; }
		public bool CreateNew { get; private set; }
		public ResolutionContext Context { get; private set; }
		private List<ServiceDependency> dependencies;

		//for construction log only
		private string[] declaredContracts;
		private readonly CacheLevel cacheLevel;
		private string comment;
		private string errorMessage;
		private Exception constructionException;

		public ContainerService(Type type, CacheLevel cacheLevel)
		{
			Type = type;
			this.cacheLevel = cacheLevel;
		}

		public void SetComment(string value)
		{
			comment = value;
		}

		public ContainerService ForFactory(IObjectAccessor arguments, bool createNew)
		{
			Arguments = arguments;
			CreateNew = createNew;
			return this;
		}

		public void AttachToContext(ResolutionContext context)
		{
			Context = context;
			declaredContracts = Context.DeclaredContractNames();
		}

		public void CheckOk()
		{
			if (status.IsGood())
				return;
			var errorTarget = SearchForError();
			if (errorTarget == null)
				throw new InvalidOperationException("assertion failure: can't find error target");
			var constructionLog = GetConstructionLog();
			var currentConstructionException = errorTarget.service != null ? errorTarget.service.constructionException : null;
			var message = currentConstructionException != null
				? string.Format("service [{0}] construction exception", errorTarget.service.Type.FormatName())
				: (errorTarget.service != null ? errorTarget.service.errorMessage : errorTarget.dependency.ErrorMessage);
			throw new SimpleContainerException(message + "\r\n\r\n" + constructionLog, currentConstructionException);
		}

		private class ErrorTarget
		{
			public ContainerService service;
			public ServiceDependency dependency;
		}

		private ErrorTarget SearchForError()
		{
			if (status == ServiceStatus.Error)
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

		public void EnsureRunCalled(LogInfo infoLogger)
		{
			if (!runCalled)
				lock (lockObject)
					if (!runCalled)
					{
						if (status == ServiceStatus.Ok)
						{
							if (dependencies != null)
								foreach (var dependency in dependencies)
									if (dependency.ContainerService != null)
										dependency.ContainerService.EnsureRunCalled(infoLogger);
							foreach (var instance in instances)
								instance.EnsureRunCalled(this, infoLogger);
						}
						runCalled = true;
					}
		}

		public void CollectInstances(Type interfaceType, CacheLevel targetCacheLevel,
			ISet<object> seen, List<NamedInstance> target)
		{
			if (cacheLevel != targetCacheLevel)
				return;
			if (dependencies != null)
				foreach (var dependency in dependencies)
					if (dependency.ContainerService != null)
						dependency.ContainerService.CollectInstances(interfaceType, targetCacheLevel, seen, target);
			foreach (var instance in instances)
			{
				var obj = instance.Instance;
				var acceptInstance = instance.Owned &&
				                     instance.CacheLevel == targetCacheLevel &&
				                     interfaceType.IsInstanceOfType(obj) &&
				                     seen.Add(obj);
				if (acceptInstance)
					target.Add(new NamedInstance(obj, new ServiceName(obj.GetType(), FinalUsedContracts)));
			}
		}

		public void AddInstance(object instance, bool owned)
		{
			instances.Add(new InstanceWrap(instance, cacheLevel, owned));
		}

		public void AddDependency(ServiceDependency dependency, bool isUnion)
		{
			if (dependencies == null)
				dependencies = new List<ServiceDependency>();
			dependencies.Add(dependency);
			status = DependencyStatusToServiceStatus(dependency.Status, isUnion);
		}

		private static ServiceStatus DependencyStatusToServiceStatus(ServiceStatus dependencyStatus, bool isUnion)
		{
			if (dependencyStatus == ServiceStatus.Error)
				return ServiceStatus.DependencyError;
			if (dependencyStatus == ServiceStatus.NotResolved && isUnion)
				return ServiceStatus.Ok;
			return dependencyStatus;
		}

		public bool LinkTo(ContainerService childService)
		{
			AddDependency(childService.GetLinkedDependency(), true);
			UnionUsedContracts(childService);
			if (status.IsBad())
				return false;
			UnionInstances(childService);
			return true;
		}

		private ServiceDependency GetLinkedDependency()
		{
			if (status == ServiceStatus.Ok)
				return ServiceDependency.Service(this, GetAllValues());
			if (status == ServiceStatus.NotResolved)
				return ServiceDependency.NotResolved(this);
			return ServiceDependency.ServiceError(this);
		}

		public int FilterInstances(Func<object, bool> filter)
		{
			return instances.RemoveAll(o => !filter(o.Instance));
		}

		public void UseAllDeclaredContracts()
		{
			FinalUsedContracts = Context.DeclaredContractNames();
			usedContractNames = FinalUsedContracts.ToList();
		}

		public void UnionUsedContracts(ContainerService dependency)
		{
			if (dependency.usedContractNames == null)
				return;
			if (usedContractNames == null)
				usedContractNames = new List<string>();
			var contractsToAdd = dependency.usedContractNames
				.Where(x => !usedContractNames.Contains(x, StringComparer.OrdinalIgnoreCase))
				.Where(Context.ContractDeclared);
			foreach (var n in contractsToAdd)
				usedContractNames.Add(n);
		}

		public void UnionStatusFrom(ContainerService containerService)
		{
			status = containerService.status;
			errorMessage = containerService.errorMessage;
			comment = containerService.comment;
			constructionException = containerService.constructionException;
		}

		public void UnionInstances(ContainerService other)
		{
			foreach (var instance in other.instances)
				if (!instances.Contains(instance))
					instances.Add(instance);
		}

		public void UnionDependencies(ContainerService other)
		{
			if (other.dependencies == null)
				return;
			if (dependencies == null)
				dependencies = new List<ServiceDependency>();
			foreach (var dependency in other.dependencies)
				if (dependency.ContainerService == null || dependencies.All(x => x.ContainerService != dependency.ContainerService))
					AddDependency(dependency, false);
		}

		public void UseContractWithName(string n)
		{
			if (usedContractNames == null)
				usedContractNames = new List<string>();
			if (!usedContractNames.Contains(n, StringComparer.OrdinalIgnoreCase))
				usedContractNames.Add(n);
		}

		public void SetError(string newErrorMessage)
		{
			errorMessage = newErrorMessage;
			status = ServiceStatus.Error;
		}

		public void SetError(Exception error)
		{
			constructionException = error;
			status = ServiceStatus.Error;
		}

		public void EndResolveDependencies()
		{
			if (FinalUsedContracts == null)
				FinalUsedContracts = GetUsedContractNamesFromContext();
		}

		public object GetSingleValue()
		{
			if (instances.Count == 1)
				return instances[0].Instance;
			var m = instances.Count == 0
				? string.Format("no implementations for [{0}]", Type.FormatName())
				: FormatManyImplementationsMessage();
			throw new SimpleContainerException(string.Format("{0}\r\n\r\n{1}", m, GetConstructionLog()));
		}

		public ServiceDependency AsSingleInstanceDependency(string dependencyName)
		{
			if (status.IsBad())
				return ServiceDependency.ServiceError(this, dependencyName);
			if (status == ServiceStatus.NotResolved)
				return ServiceDependency.NotResolved(this, dependencyName);
			return instances.Count > 1
				? ServiceDependency.Error(this, dependencyName, FormatManyImplementationsMessage())
				: ServiceDependency.Service(this, instances[0].Instance, dependencyName);
		}

		public IEnumerable<object> GetAllValues()
		{
			return typedArray ?? (typedArray = instances.Select(x => x.Instance).CastToObjectArrayOf(Type));
		}

		public string[] GetUsedContractNames()
		{
			return FinalUsedContracts ?? GetUsedContractNamesFromContext();
		}

		private string[] GetUsedContractNamesFromContext()
		{
			return usedContractNames == null
				? InternalHelpers.emptyStrings
				: Context.GetDeclaredContractsByNames(usedContractNames);
		}

		private string FormatManyImplementationsMessage()
		{
			return string.Format("many implementations for [{0}]\r\n{1}", Type.FormatName(),
				instances.Select(x => "\t" + x.Instance.GetType().FormatName()).JoinStrings("\r\n"));
		}

		public bool WaitForResolve()
		{
			if (!instantiated)
				lock (lockObject)
					while (!instantiated)
						Monitor.Wait(lockObject);
			if (FinalUsedContracts == null)
			{
				const string messageFormat = "assertion failure: FinalUsedContracts == null, type [{0}]";
				throw new InvalidOperationException(string.Format(messageFormat, Type));
			}
			return status.IsGood();
		}

		public void WaitForResolveOrDie()
		{
			if (!WaitForResolve())
				CheckOk();
		}

		public bool AcquireInstantiateLock()
		{
			if (instantiated)
				return false;
			Monitor.Enter(lockObject);
			if (!instantiated)
				return true;
			Monitor.Exit(lockObject);
			return false;
		}

		public void ReleaseInstantiateLock()
		{
			instantiated = true;
			Monitor.PulseAll(lockObject);
			Monitor.Exit(lockObject);
		}

		public void EndInstantiate()
		{
			EndResolveDependencies();
			if (status == ServiceStatus.Ok && instances.Count == 0)
				status = ServiceStatus.NotResolved;
		}

		public string GetConstructionLog()
		{
			var writer = new SimpleTextLogWriter();
			WriteConstructionLog(writer);
			return writer.GetText();
		}

		public void WriteConstructionLog(ISimpleLogWriter writer)
		{
			WriteConstructionLog(new ConstructionLogContext(writer));
		}

		public void WriteConstructionLog(ConstructionLogContext context)
		{
			var usedContracts = GetUsedContractNames();
			var attentionRequired = status.IsBad() ||
			                        status == ServiceStatus.NotResolved && (context.UsedFromDependency == null ||
			                                                                context.UsedFromDependency.Status ==
			                                                                ServiceStatus.NotResolved);
			if (attentionRequired)
				context.Writer.WriteMeta("!");
			var formattedName = context.UsedFromDependency == null ? Type.FormatName() : context.UsedFromDependency.Name;
			context.Writer.WriteName(cacheLevel == CacheLevel.Static ? "(s)" + formattedName : formattedName);
			if (usedContracts != null && usedContracts.Length > 0)
				context.Writer.WriteUsedContract(InternalHelpers.FormatContractsKey(usedContracts));
			var previousContractsKey =
				InternalHelpers.FormatContractsKey(context.UsedFromService == null
					? null
					: context.UsedFromService.declaredContracts);
			var currentContractsKey = InternalHelpers.FormatContractsKey(declaredContracts);
			if (previousContractsKey != currentContractsKey && !string.IsNullOrEmpty(currentContractsKey))
			{
				context.Writer.WriteMeta("->[");
				context.Writer.WriteMeta(currentContractsKey);
				context.Writer.WriteMeta("]");
			}
			if (instances.Count > 1)
				context.Writer.WriteMeta("++");
			if (status == ServiceStatus.Error)
				context.Writer.WriteMeta(" <---------------");
			else if (comment != null)
			{
				context.Writer.WriteMeta(" - ");
				context.Writer.WriteMeta(comment);
			}
			if (context.UsedFromDependency != null && context.UsedFromDependency.Status == ServiceStatus.Ok &&
			    context.UsedFromDependency.Value == null)
				context.Writer.WriteMeta(" = <null>");
			context.Writer.WriteNewLine();
			if (context.Seen.Add(new ServiceName(Type, usedContracts)) && dependencies != null)
				foreach (var d in dependencies)
				{
					context.UsedFromService = this;
					context.Indent++;
					d.WriteConstructionLog(context);
					context.Indent--;
				}
		}

		private class InstanceWrap
		{
			private bool runCalled;
			public object Instance { get; private set; }
			public CacheLevel CacheLevel { get; private set; }
			public bool Owned { get; set; }

			public InstanceWrap(object instance, CacheLevel cacheLevel, bool owned)
			{
				Instance = instance;
				CacheLevel = cacheLevel;
				Owned = owned;
			}

			public override bool Equals(object obj)
			{
				if (ReferenceEquals(null, obj)) return false;
				if (ReferenceEquals(this, obj)) return true;
				if (obj.GetType() != GetType()) return false;
				return ReferenceEquals(Instance, ((InstanceWrap) obj).Instance);
			}

			public void EnsureRunCalled(ContainerService service, LogInfo infoLogger)
			{
				var componentInstance = Instance as IComponent;
				if (componentInstance == null)
					return;
				if (!runCalled)
					lock (this)
						if (!runCalled)
						{
							var name = new ServiceName(Instance.GetType(), service.FinalUsedContracts);
							if (infoLogger != null)
								infoLogger(name, "run started");
							try
							{
								componentInstance.Run();
							}
							catch (Exception e)
							{
								throw new SimpleContainerException(string.Format("exception running {0}", name), e);
							}
							if (infoLogger != null)
								infoLogger(name, "run finished");
							runCalled = true;
						}
			}

			public override int GetHashCode()
			{
				return Instance.GetHashCode();
			}
		}
	}
}