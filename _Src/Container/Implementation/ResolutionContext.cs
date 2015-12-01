using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class ResolutionContext
	{
		[ThreadStatic] private static ResolutionContext current;
		private ResolutionContext prev;

		private ResolutionContext()
		{
		}

		public static ResolutionContextActivation Push(SimpleContainer container)
		{
			var prev = current;
			return new ResolutionContextActivation
			{
				activated = prev != null && prev.Container == container
					? prev
					: current = new ResolutionContext
					{
						Container = container,
						prev = prev,
						Stack = new List<ContainerService.Builder>(),
						ConstructingServices = new HashSet<ServiceName>(),
						Contracts = new ContractsList(),
						AnalizeDependenciesOnly = prev != null && prev.AnalizeDependenciesOnly
					},
				previous = prev
			};
		}

		public static void Pop(ResolutionContextActivation activation)
		{
			current = activation.previous;
		}

		public struct ResolutionContextActivation
		{
			public ResolutionContext activated;
			public ResolutionContext previous;
		}

		public static bool HasPendingResolutionContext
		{
			get { return current != null; }
		}

		public SimpleContainer Container { get; private set; }
		public List<ContainerService.Builder> Stack { get; private set; }
		public HashSet<ServiceName> ConstructingServices { get; private set; }
		public ContractsList Contracts { get; private set; }
		//piece of shit, kill
		public bool AnalizeDependenciesOnly { get; set; }

		public ContainerService.Builder TopBuilder
		{
			get { return Stack[Stack.Count - 1]; }
		}

		public bool HasCycle(ServiceName name)
		{
			var context = this;
			while (context != null)
			{
				if (context.Container == Container && context.ConstructingServices.Contains(name))
					return true;
				context = context.prev;
			}
			return false;
		}

		public string FormatStack()
		{
			var contexts = new List<ResolutionContext>();
			var context = this;
			while (context != null)
			{
				contexts.Add(context);
				context = context.prev;
			}
			var result = new StringBuilder();
			for (var i = contexts.Count - 1; i >= 0; i--)
			{
				var stackItems = contexts[i].Stack.Select(y => "\t" + y.Name.ToString()).ToArray();
				if (i != contexts.Count - 1)
				{
					result.AppendLine();
					if (contexts[i + 1].Container != contexts[i].Container)
						stackItems[0] = stackItems[0] + "[container boundary]";
				}
				result.Append(stackItems.JoinStrings("\r\n"));
			}
			return result.ToString();
		}
	}
}