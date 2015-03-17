using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class ContainerService
	{
		public ServiceStatus status;
		private List<string> usedContractNames;
		private readonly List<object> instances = new List<object>();
		private IEnumerable<object> typedArray;
		private readonly object lockObject = new object();
		private bool instantiated;
		private volatile bool runCalled;
		public Type Type { get; private set; }
		public int TopSortIndex { get; private set; }
		public List<string> FinalUsedContracts { get; private set; }
		public IObjectAccessor Arguments { get; private set; }
		public bool CreateNew { get; private set; }
		public ResolutionContext Context { get; private set; }
		private List<ServiceDependency> dependencies;

		//for construction log only
		private List<string> declaredContracts;
		private readonly bool isStatic;
		private string message;
		private string errorMessage;
		private Exception constructionException;

		public ContainerService(Type type, bool isStatic)
		{
			Type = type;
			this.isStatic = isStatic;
		}

		public void SetMessage(string newMessage)
		{
			message = newMessage;
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

		public IEnumerable<object> AsEnumerable()
		{
			return typedArray ?? (typedArray = instances.CastToObjectArrayOf(Type));
		}

		public ServiceDependency GetDependency(ContainerService containerService)
		{
			return dependencies.Single(x => x.ContainerService == containerService);
		}

		public void CheckOk()
		{
			if (status.IsGood())
				return;
			var targetErrorMessage = SearchForError();
			if (targetErrorMessage == null)
				throw new InvalidOperationException("assertion failure");
			var constructionLog = GetConstructionLog();
			throw new SimpleContainerException(targetErrorMessage + "\r\n" + constructionLog);
		}

		private string SearchForError()
		{
			if (status == ServiceStatus.Error)
				return errorMessage;
			if (dependencies != null)
				foreach (var dependency in dependencies)
				{
					if (dependency.Status == ServiceStatus.Error)
						return dependency.ErrorMessage;
					if (dependency.ContainerService != null)
					{
						var result = dependency.ContainerService.SearchForError();
						if (result != null)
							return result;
					}
				}
			return null;
		}

		public void EnsureRunCalled(ComponentsRunner runner, bool useCache)
		{
			if (!runCalled)
				lock (lockObject)
					if (!runCalled)
					{
						if (dependencies != null)
							foreach (var dependency in dependencies)
								if (dependency.ContainerService != null)
									dependency.ContainerService.EnsureRunCalled(runner, true);
						runner.EnsureRunCalled(this, useCache);
						runCalled = true;
					}
		}

		public IEnumerable<ServiceInstance> GetInstances()
		{
			return Instances.Select(y => new ServiceInstance(y, this));
		}

		public void AddInstance(object instance)
		{
			instances.Add(instance);
		}

		public void AddDependency(ServiceDependency dependency)
		{
			if (dependencies == null)
				dependencies = new List<ServiceDependency>();
			dependencies.Add(dependency);
			status = dependency.Status;
		}

		public IReadOnlyList<object> Instances
		{
			get { return instances; }
		}

		public void FilterInstances(Func<object, bool> filter)
		{
			instances.RemoveAll(o => !filter(o));
		}

		public void UseAllDeclaredContracts()
		{
			FinalUsedContracts = Context.DeclaredContractNames();
			usedContractNames = FinalUsedContracts;
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
					AddDependency(dependency);
		}

		public void UseContractWithName(string n)
		{
			if (usedContractNames == null)
				usedContractNames = new List<string>();
			if (!usedContractNames.Contains(n, StringComparer.OrdinalIgnoreCase))
				usedContractNames.Add(n);
		}

		public void EndResolveDependencies()
		{
			FinalUsedContracts = GetUsedContractNamesFromContext();
		}

		public void EndResolveDependenciesWithError(string newErrorMessage)
		{
			EndResolveDependencies();
			errorMessage = newErrorMessage;
			status = ServiceStatus.Error;
		}

		public void EndResolveDependenciesWithError(Exception error)
		{
			EndResolveDependencies();
			constructionException = error;
			status = ServiceStatus.Error;
		}

		public List<string> GetUsedContractNames()
		{
			return FinalUsedContracts ?? GetUsedContractNamesFromContext();
		}

		private List<string> GetUsedContractNamesFromContext()
		{
			return usedContractNames == null
				? new List<string>(0)
				: Context.GetDeclaredContractsByNames(usedContractNames);
		}

		public string FormatManyImplementationsMessage()
		{
			return string.Format("many implementations for [{0}]\r\n{1}", Type.FormatName(),
				instances.Select(x => "\t" + x.GetType().FormatName()).JoinStrings("\r\n"));
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

		public void InstantiatedSuccessfully(int topSortIndex)
		{
			TopSortIndex = topSortIndex;
			instantiated = true;
		}

		public void ReleaseInstantiateLock()
		{
			Monitor.PulseAll(lockObject);
			Monitor.Exit(lockObject);
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
			var formattedName = Type.FormatName();
			context.Writer.WriteName(isStatic ? "(s)" + formattedName : formattedName);
			var usedContracts = GetUsedContractNames();
			if (usedContracts != null && usedContracts.Count > 0)
				context.Writer.WriteUsedContract(InternalHelpers.FormatContractsKey(usedContracts));
			if (context.UsedFromService == null || declaredContracts.Count > context.UsedFromService.declaredContracts.Count)
			{
				context.Writer.WriteMeta("->[");
				context.Writer.WriteMeta(InternalHelpers.FormatContractsKey(declaredContracts));
				context.Writer.WriteMeta("]");
			}
			if (Instances.Count == 0)
				context.Writer.WriteMeta("!");
			else if (Instances.Count > 1)
				context.Writer.WriteMeta("++");
			if (context.UsedFromDependency != null && context.UsedFromDependency.Value == null)
				context.Writer.WriteMeta(" -> <null>");
			if (message != null)
			{
				context.Writer.WriteMeta(" - ");
				context.Writer.WriteMeta(message);
			}
			context.Writer.WriteNewLine();
			if (context.Seen.Add(new CacheKey(Type, usedContracts)) && dependencies != null)
				foreach (var d in dependencies)
				{
					context.UsedFromService = this;
					d.WriteConstructionLog(context);
				}
		}
	}
}