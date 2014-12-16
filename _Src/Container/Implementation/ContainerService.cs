using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;

namespace SimpleContainer.Implementation
{
	//todo распилить эту помойку
	//todo придумать нормальную абстракцию над стеком контрактов + usedContracts
	internal class ContainerService
	{
		private List<int> usedContractIndexes;
		private readonly List<object> instances = new List<object>();
		private IEnumerable<object> typedArray;
		private readonly object lockObject = new object();
		private bool instantiated;
		private bool failed;
		public Type Type { get; private set; }
		public int TopSortIndex { get; private set; }
		public List<string> FinalUsedContracts { get; private set; }
		public IObjectAccessor Arguments { get; private set; }
		public bool CreateNew { get; private set; }
		public ResolutionContext Context { get; private set; }
		public List<ContainerService> Dependencies { get; private set; }

		public ContainerService(Type type)
		{
			Type = type;
		}

		public static ContainerService ForFactory(Type type, object arguments)
		{
			return new ContainerService(type) {CreateNew = true}.WithArguments(ObjectAccessor.Get(arguments));
		}

		public IEnumerable<ServiceInstance<object>> GetInstances()
		{
			return Instances.Select(y => new ServiceInstance<object>(y, InternalHelpers.FormatContractsKey(FinalUsedContracts)));
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

		public void AddInstance(object instance)
		{
			instances.Add(instance);
		}

		public IReadOnlyList<object> Instances
		{
			get { return instances; }
		}

		public void FilterInstances(Func<object, bool> filter)
		{
			instances.RemoveAll(o => !filter(o));
		}

		public void UseAllRequiredContracts()
		{
			FinalUsedContracts = Context.requiredContracts.Select(x => x.Name).ToList();
			usedContractIndexes = Enumerable.Range(0, FinalUsedContracts.Count).Select((i, x) => i).ToList();
		}

		public void UnionUsedContracts(ContainerService dependency)
		{
			if (dependency.usedContractIndexes == null)
				return;
			if (usedContractIndexes == null)
				usedContractIndexes = new List<int>();
			foreach (var otherIndex in dependency.usedContractIndexes)
				if (otherIndex < Context.requiredContracts.Count && !usedContractIndexes.Contains(otherIndex))
					usedContractIndexes.Add(otherIndex);
		}

		public void UnionFrom(ContainerService other)
		{
			foreach (var instance in other.instances)
				if (!instances.Contains(instance))
					instances.Add(instance);
			UnionUsedContracts(other);
		}

		public void UseContractWithIndex(int index)
		{
			if (usedContractIndexes == null)
				usedContractIndexes = new List<int>();
			if (!usedContractIndexes.Contains(index))
				usedContractIndexes.Add(index);
		}

		public void EndResolveDependencies()
		{
			FinalUsedContracts = GetUsedContractNamesFromContext();
		}

		public void SetDependencies(List<ContainerService> dependencies)
		{
			Dependencies = dependencies;
		}

		public List<string> GetUsedContractNames()
		{
			return FinalUsedContracts ?? GetUsedContractNamesFromContext();
		}

		private List<string> GetUsedContractNamesFromContext()
		{
			return usedContractIndexes == null
				? new List<string>(0)
				: usedContractIndexes.OrderBy(x => x).Select(i => Context.requiredContracts[i].Name).ToList();
		}

		public object SingleInstance(bool inConstructor)
		{
			if (instances.Count == 1)
				return instances[0];
			if (instances.Count == 0 && inConstructor)
				throw new ServiceCouldNotBeCreatedException();
			var prefix = instances.Count == 0
				? "no implementations for " + Type.Name
				: string.Format("many implementations for {0}\r\n{1}", Type.Name,
					instances.Select(x => "\t" + x.GetType().FormatName()).JoinStrings("\r\n"));
			throw new SimpleContainerException(string.Format("{0}\r\n{1}", prefix, Context.Format()));
		}

		public bool WaitForSuccessfullResolve()
		{
			if (!instantiated && !failed)
				lock (lockObject)
					while (!instantiated && !failed)
						Monitor.Wait(lockObject);
			if (!failed && FinalUsedContracts == null)
			{
				const string messageFormat = "assertion failure: FinalUsedContracts == null, type [{0}]";
				throw new InvalidOperationException(string.Format(messageFormat, Type));
			}
			return instantiated;
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
			failed = false;
		}

		public void InstantiatedUnsuccessfully()
		{
			failed = true;
		}

		public void ReleaseInstantiateLock()
		{
			Monitor.PulseAll(lockObject);
			Monitor.Exit(lockObject);
		}

		public void Throw(string format, params object[] args)
		{
			Context.Report("<---------------");
			Context.Throw(format, args);
		}
	}
}