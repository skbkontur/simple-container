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
		private readonly object lockObject = new object();
		private string comment;
		private Exception constructionException;
		private ServiceDependency[] dependencies;
		internal volatile bool disposing;
		private volatile bool initialized;
		private volatile bool initializing;
		public InstanceWrap[] Instances { get; private set; }

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

		public void CheckSingleValue(ContainerContext containerContext)
		{
			if (Instances.Length == 1)
				return;
			string message;
			if (Instances.Length == 0)
			{
				var targetType = Type.IsDelegate() ? Type.DeclaringType : Type;
				message = $"no instances for [{targetType.FormatName()}]";
				var notResolvedRoot = SearchForNotResolvedRoot();
				if (notResolvedRoot != this)
					message += $" because [{notResolvedRoot.Type.FormatName()}] has no instances";
			}
			else
				message = FormatManyImplementationsMessage();
			var assemblies = containerContext.typesList.GetAssemblies()
				.OrderBy(x => x.GetName().Name)
				.Select(x => "\t" + x.GetName().Name).JoinStrings(Environment.NewLine);
			throw new SimpleContainerException(message
				+ Environment.NewLine + GetConstructionLog(containerContext)
				+ Environment.NewLine + "scanned assemblies"
				+ Environment.NewLine + assemblies);
		}

		public ServiceDependency AsDependency(ContainerContext containerContext, string dependencyName, bool isEnumerable)
		{
			if (Status.IsBad())
				return containerContext.ServiceError(this, dependencyName);
			if (Status == ServiceStatus.NotResolved)
				return containerContext.NotResolved(this, dependencyName);
			if (!isEnumerable)
				return Instances.Length > 1
					? containerContext.Error(this, dependencyName, FormatManyImplementationsMessage())
					: containerContext.Service(this, Instances[0].Instance, dependencyName);
			return containerContext.Service(this, GetAllValues(), dependencyName);
		}

		public IEnumerable<object> GetAllValues()
		{
			return typedArray ?? (typedArray = Instances.Select(x => x.Instance).CastToObjectArrayOf(Type));
		}

		[ThreadStatic] private static bool threadInitializing;

		public static bool ThreadInitializing
		{
			get { return threadInitializing; }
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
						var oldInitializingService = threadInitializing;
						threadInitializing = true;
						try
						{
							if (dependencies != null)
								foreach (var dependency in dependencies)
									if (dependency.ContainerService != null)
										dependency.ContainerService.EnsureInitialized(containerContext, root);
							foreach (var instance in Instances)
								instance.EnsureInitialized(this, containerContext, root);
							initialized = true;
						}
						finally
						{
							initializing = false;
							threadInitializing = oldInitializingService;
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
			foreach (var instance in Instances)
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
			var name = new ServiceName(Type, usedContracts);
			if (!context.Seen.Add(name))
			{
				if (context.UsedFromService != null && context.UsedFromService.Name.Equals(name) &&
				    context.UsedFromDependency != null && context.UsedFromDependency.Comment != null)
				{
					context.Writer.WriteMeta(" - ");
					context.Writer.WriteMeta(context.UsedFromDependency.Comment);
				}
				context.Writer.WriteNewLine();
				return;
			}
			if (Instances.Length > 1)
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
			else if (Instances.Length == 1 && Instances[0].IsConstant)
				ValueFormatter.WriteValue(context, Instances[0].Instance, true);
			if (initializing)
				context.Writer.WriteMeta(", initializing ...");
			else if (disposing)
				context.Writer.WriteMeta(", disposing ...");
			context.Writer.WriteNewLine();
			if (dependencies != null)
				foreach (var d in dependencies)
				{
					context.Indent++;
					context.UsedFromService = this;
					d.WriteConstructionLog(context);
					context.Indent--;
				}
		}

		public void CheckStatusIsGood(ContainerContext containerContext)
		{
			if (Status.IsGood())
				return;
			var errorTarget = SearchForError();
			if (errorTarget == null)
				throw new InvalidOperationException("assertion failure: can't find error target");
			var constructionLog = GetConstructionLog(containerContext);
			var currentConstructionException = errorTarget.service?.constructionException;
			var message = currentConstructionException != null
				? $"service [{errorTarget.service.Type.FormatName()}] construction exception"
				: (errorTarget.service != null ? errorTarget.service.comment : errorTarget.dependency.Comment);
			var exceptionMessage = message + Environment.NewLine + Environment.NewLine + constructionLog;
			throw new SimpleContainerException(exceptionMessage, currentConstructionException);
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

		private string FormatManyImplementationsMessage()
		{
			return string.Format("many instances for [{0}]{1}{2}",
				Type.FormatName(),
				Environment.NewLine,
				Instances.Select(x => "\t" + x.Instance.GetType().FormatName()).JoinStrings(Environment.NewLine));
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
			public string DependencyName { get; set; }
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

			public ExpandedUnions? ExpandedUnions { get; set; }

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

			public ServiceName GetFinalName()
			{
				return new ServiceName(Type, FinalUsedContracts);
			}

			public ServiceName GetDeclaredName()
			{
				return new ServiceName(Type, DeclaredContracts);
			}

			public void AddInstance(object instance, bool owned, bool isConstant)
			{
				AddInstance(new InstanceWrap(instance, owned, isConstant));
			}

			private void AddInstance(InstanceWrap wrap)
			{
				if (Configuration != null && Configuration.InstanceFilter != null && !Configuration.InstanceFilter(wrap.Instance))
					SetComment("instance filter");
				else
					instances.Add(wrap);
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
				var dependency = childService.AsDependency(containerContext, null, true);
				dependency.Comment = comment;
				AddDependency(dependency, true);
				UnionUsedContracts(childService);
				if (target.Status.IsGood())
					foreach (var instance in childService.Instances)
						if (!instances.Contains(instance))
							AddInstance(instance);
			}

			public void UnionUsedContracts(ContainerService dependency)
			{
				if (dependency.UsedContracts == null)
					return;
				if (usedContractNames == null)
					usedContractNames = new List<string>();
				foreach (var dependencyContract in dependency.UsedContracts)
				{
					if (usedContractNames.ContainsIgnoringCase(dependencyContract))
						continue;
					string usedContractName = null;
					if (DeclaredContracts.ContainsIgnoringCase(dependencyContract))
						usedContractName = dependencyContract;
					else if (ExpandedUnions.HasValue)
						foreach (var c in ExpandedUnions.Value.unionedContracts)
							if (c.children.ContainsIgnoringCase(dependencyContract))
							{
								usedContractName = c.parent;
								break;
							}
					if (usedContractName != null)
						usedContractNames.Add(usedContractName);
				}
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
				target.Instances = instances.ToArray();
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

			public void CreateInstanceBy(CallTarget callTarget, bool owned)
			{
				object instance;
				try
				{
					instance = callTarget.method == null
						? callTarget.factory(this)
						: callTarget.method.Compile()(callTarget.self, callTarget.actualArguments);
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
				AddInstance(instance, owned, false);
			}
		}
	}
}