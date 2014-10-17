using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SimpleContainer.Helpers;
using SimpleContainer.Reflection;

namespace SimpleContainer
{
	public class ContainerService
	{
		public Type type;
		public ResolutionContext context;
		public bool contractUsed;
		public object lockObject = new object();
		public readonly List<object> instances = new List<object>();
		public bool resolved;
		private IEnumerable<object> typedArray;
		private static readonly TimeSpan waitTimeout = TimeSpan.FromSeconds(5);

		public IEnumerable<object> AsEnumerable()
		{
			return typedArray ?? (typedArray = instances.CastToObjectArrayOf(type));
		}

		public object SingleInstance()
		{
			if (instances.Count == 1)
				return instances[0];
			var prefix = instances.Count == 0
							 ? "no implementations for " + type.Name
							 : string.Format("many implementations for {0}\r\n{1}", type.Name, instances.Select(x => "\t" + x.GetType().FormatName()).JoinStrings("\r\n"));
			throw new SimpleContainerException(string.Format("{0}\r\n{1}", prefix, context.Format(type)));
		}

		public ContainerService WaitForResolve()
		{
			if (!resolved)
				lock (lockObject)
					while (!resolved)
						if (!Monitor.Wait(lockObject, waitTimeout))
							throw new SimpleContainerException(string.Format("service [{0}] wait for resolve timed out after [{1}] millis",
																			 type.FormatName(), waitTimeout.TotalMilliseconds));
			return this;
		}

		public void SetResolved()
		{
			resolved = true;
			Monitor.PulseAll(lockObject);
		}

		public void Throw(string format, params object[] args)
		{
			context.Report("<---------------");
			context.Throw(format, args);
		}
	}
}