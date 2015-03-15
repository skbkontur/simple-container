using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
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
		private ExceptionDispatchInfo exception;
		private volatile bool runCalled;
		public Type Type { get; private set; }
		public int TopSortIndex { get; private set; }
		public List<string> FinalUsedContracts { get; private set; }
		public IObjectAccessor Arguments { get; private set; }
		public bool CreateNew { get; private set; }
		public ResolutionContext Context { get; private set; }
		private List<ContainerService> dependencies;

		//trash
		public string name;
		public List<string> declaredContracts;
		public bool isStatic;
		public string message;
		public Exception constructionException;

		public ContainerService(Type type)
		{
			Type = type;
		}

		public static ContainerService ForFactory(Type type, object arguments)
		{
			return new ContainerService(type) {CreateNew = true}.WithArguments(ObjectAccessor.Get(arguments));
		}

		public ContainerService WithArguments(IObjectAccessor arguments)
		{
			Arguments = arguments;
			return this;
		}

		public void AttachToContext(ResolutionContext context)
		{
			Context = context;
		}

		public IEnumerable<object> AsEnumerable()
		{
			return typedArray ?? (typedArray = instances.CastToObjectArrayOf(Type));
		}

		public void EnsureRunCalled(ComponentsRunner runner, bool useCache)
		{
			if (!runCalled)
				lock (lockObject)
					if (!runCalled)
					{
						if (dependencies != null)
							foreach (var dependency in dependencies)
								dependency.EnsureRunCalled(runner, true);
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

		public void AddDependency(ContainerService dependency)
		{
			if (dependencies == null)
				dependencies = new List<ContainerService>();
			if (!dependencies.Contains(dependency))
				dependencies.Add(dependency);
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

		public void UnionFrom(ContainerService other)
		{
			foreach (var instance in other.instances)
				if (!instances.Contains(instance))
					instances.Add(instance);
			UnionDependencies(other);
			UnionUsedContracts(other);
		}

		public void UnionDependencies(ContainerService other)
		{
			if (other.dependencies != null)
				foreach (var dependency in other.dependencies)
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
			status = ServiceStatus.Ok;
		}
		
		public void EndResolveDependenciesWithFailure(string failureMessage)
		{
			FinalUsedContracts = GetUsedContractNamesFromContext();
			message = failureMessage;
			status = ServiceStatus.Failed;
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

		public object SingleInstance(bool inConstructor)
		{
			if (instances.Count == 1)
				return instances[0];
			if (instances.Count == 0 && inConstructor)
				throw new ServiceCouldNotBeCreatedException();
			var prefix = SingleInstanceViolationMessage();
			throw new SimpleContainerException(string.Format("{0}\r\n{1}", prefix, Context.Format()));
		}

		public string SingleInstanceViolationMessage()
		{
			return instances.Count == 0
				? "no implementations for " + Type.FormatName()
				: string.Format("many implementations for {0}\r\n{1}", Type.FormatName(),
					instances.Select(x => "\t" + x.GetType().FormatName()).JoinStrings("\r\n"));
		}

		public bool WaitForResolve()
		{
			if (!instantiated && exception == null)
				lock (lockObject)
					while (!instantiated && exception == null)
						Monitor.Wait(lockObject);
			if (exception == null && FinalUsedContracts == null)
			{
				const string messageFormat = "assertion failure: FinalUsedContracts == null, type [{0}]";
				throw new InvalidOperationException(string.Format(messageFormat, Type));
			}
			return instantiated;
		}

		public void WaitForResolveOrDie()
		{
			if (!WaitForResolve())
				exception.Throw();
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
			exception = null;
		}

		public void InstantiatedUnsuccessfully(Exception e)
		{
			exception = ExceptionDispatchInfo.Capture(e);
		}

		public void ReleaseInstantiateLock()
		{
			Monitor.PulseAll(lockObject);
			Monitor.Exit(lockObject);
		}

		public void Throw(string format, params object[] args)
		{
			Context.Comment("<---------------");
			Context.Throw(format, args);
		}

		public string Format()
		{
			var writer = new SimpleTextLogWriter();
			Format(null, writer);
			return writer.GetText();
		}

		public void Format(ISimpleLogWriter writer)
		{
			var startDepth = 0;
			var targetTypeFound = false;
			foreach (var state in log)
			{
				if (containerService != null && state.service != containerService && !targetTypeFound)
					continue;
				if (targetTypeFound && state.depth <= startDepth)
					break;
				if (!targetTypeFound)
				{
					targetTypeFound = true;
					startDepth = state.depth;
				}
				writer.WriteIndent(state.depth - startDepth);
				var isSimpleType = state.service.Type.IsSimpleType();
				var name = state.name != null && isSimpleType ? state.name : state.service.Type.FormatName();
				writer.WriteName(state.isStatic ? "(s)" + name : name);
				var usedContracts = state.service.GetUsedContractNames();
				if (usedContracts != null && usedContracts.Count > 0)
					writer.WriteUsedContract(InternalHelpers.FormatContractsKey(usedContracts));
				if (state.declaredContacts != null && state.contractDeclared)
				{
					writer.WriteMeta("->[");
					writer.WriteMeta(state.declaredContacts);
					writer.WriteMeta("]");
				}
				if (state.service.Instances.Count == 0)
					writer.WriteMeta("!");
				else if (state.service.Instances.Count > 1)
					writer.WriteMeta("++");
				else if (isSimpleType)
					writer.WriteMeta(" -> " + (state.service.Instances[0] ?? "<null>"));
				if (state.message != null)
				{
					writer.WriteMeta(" - ");
					writer.WriteMeta(state.message);
				}
				writer.WriteNewLine();
			}
		}
	}
}