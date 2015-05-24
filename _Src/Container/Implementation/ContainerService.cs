using System;
using System.Collections.Generic;
using System.Linq;
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
		private readonly object lockObject = new object();

		private object[] typedArray;
		private volatile bool runCalled;
		private CacheLevel cacheLevel;
		private string comment;
		private string errorMessage;
		private Exception constructionException;
		private ServiceDependency[] dependencies;
		private InstanceWrap[] instances;

		private ContainerService()
		{
		}

		public object GetSingleValue()
		{
			CheckOk();
			if (instances.Length == 1)
				return instances[0].Instance;
			var m = instances.Length == 0
				? string.Format("no implementations for [{0}]", Type.FormatName())
				: FormatManyImplementationsMessage();
			throw new SimpleContainerException(string.Format("{0}\r\n\r\n{1}", m, GetConstructionLog()));
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
			CheckOk();
			return typedArray ?? (typedArray = instances.Select(x => x.Instance).CastToObjectArrayOf(Type));
		}

		public void EnsureRunCalled(LogInfo infoLogger)
		{
			if (Status != ServiceStatus.Ok)
				return;
			if (!runCalled)
				lock (lockObject)
					if (!runCalled)
					{
						if (dependencies != null)
							foreach (var dependency in dependencies)
								if (dependency.ContainerService != null)
									dependency.ContainerService.EnsureRunCalled(infoLogger);
						foreach (var instance in instances)
							instance.EnsureRunCalled(this, infoLogger);
						runCalled = true;
					}
		}

		public void CollectInstances(Type interfaceType, CacheLevel targetCacheLevel,
			ISet<object> seen, List<NamedInstance> target)
		{
			if (Status.IsBad())
				return;
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
					target.Add(new NamedInstance(obj, new ServiceName(obj.GetType(), UsedContracts)));
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
			WriteConstructionLog(new ConstructionLogContext(writer));
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
			context.Writer.WriteName(cacheLevel == CacheLevel.Static ? "(s)" + formattedName : formattedName);
			if (usedContracts != null && usedContracts.Length > 0)
				context.Writer.WriteUsedContract(InternalHelpers.FormatContractsKey(usedContracts));
			if (instances.Length > 1)
				context.Writer.WriteMeta("++");
			if (Status == ServiceStatus.Error)
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
					context.Indent++;
					d.WriteConstructionLog(context);
					context.Indent--;
				}
		}

		private void CheckOk()
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
			return string.Format("many implementations for [{0}]\r\n{1}", Type.FormatName(),
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
			private bool borrowed;
			public ResolutionContext Context { get; private set; }
			public SimpleContainer Container { get; private set; }

			public Builder(Type type, SimpleContainer container, ResolutionContext context)
			{
				target = new ContainerService
				{
					Type = type,
					cacheLevel = container.CacheLevel
				};
				Context = context;
				Container = container;
				DeclaredContracts = context.Contracts.ToArray();
				try
				{
					Configuration = container.GetConfiguration(Type, context);
				}
				catch (Exception e)
				{
					SetError(e);
					return;
				}
				foreach (var contract in Configuration.Contracts)
				{
					if (usedContractNames == null)
						usedContractNames = new List<string>();
					if (!usedContractNames.Contains(contract, StringComparer.OrdinalIgnoreCase))
						usedContractNames.Add(contract);
				}
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

			public void AddInstance(object instance, bool owned)
			{
				instances.Add(new InstanceWrap(instance, target.cacheLevel, owned));
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

			public Builder ForFactory(IObjectAccessor arguments, bool createNew)
			{
				Arguments = arguments;
				CreateNew = createNew;
				return this;
			}

			public bool LinkTo(ContainerService childService)
			{
				AddDependency(childService.GetLinkedDependency(), true);
				UnionUsedContracts(childService);
				if (target.Status.IsBad())
					return false;
				foreach (var instance in childService.instances)
					if (!instances.Contains(instance))
						instances.Add(instance);
				return true;
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
				target.errorMessage = containerService.errorMessage;
				target.comment = containerService.comment;
				target.constructionException = containerService.constructionException;
			}

			public void SetError(string newErrorMessage)
			{
				target.errorMessage = newErrorMessage;
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
					target.UsedContracts = GetUsedContractNamesFromContext();
			}

			public void Borrow(ContainerService containerService)
			{
				target = containerService;
				borrowed = true;
			}

			public ContainerService Build()
			{
				if (borrowed)
					return target;
				EndResolveDependencies();
				if (target.Status == ServiceStatus.Ok && instances.Count == 0)
					target.Status = ServiceStatus.NotResolved;
				target.instances = instances.ToArray();
				if (dependencies != null)
					target.dependencies = dependencies.ToArray();
				return target;
			}

			private string[] GetUsedContractNamesFromContext()
			{
				return usedContractNames == null
					? InternalHelpers.emptyStrings
					: DeclaredContracts
						.Where(x => usedContractNames.Contains(x, StringComparer.OrdinalIgnoreCase))
						.ToArray();
			}

			private static ServiceStatus DependencyStatusToServiceStatus(ServiceStatus dependencyStatus, bool isUnion)
			{
				if (dependencyStatus == ServiceStatus.Error)
					return ServiceStatus.DependencyError;
				if (dependencyStatus == ServiceStatus.NotResolved && isUnion)
					return ServiceStatus.Ok;
				return dependencyStatus;
			}
		}
	}
}