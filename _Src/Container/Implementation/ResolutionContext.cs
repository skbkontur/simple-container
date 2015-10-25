using System;
using System.Collections.Generic;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class ResolutionContext
	{
		private readonly List<ContainerService.Builder> stack = new List<ContainerService.Builder>();
		private readonly HashSet<ServiceName> constructingServices = new HashSet<ServiceName>();

		public List<string> Contracts { get; private set; }
		public SimpleContainer Container { get; private set; }

		public bool AnalizeDependenciesOnly { get; set; }

		public ResolutionContext(SimpleContainer container, string[] contracts)
		{
			Container = container;
			Contracts = new List<string>(contracts);
		}

		public ContainerService Create(Type type, List<string> contracts, object arguments)
		{
			List<string> oldContracts = null;
			if (contracts != null)
			{
				oldContracts = Contracts;
				Contracts = contracts;
			}
			var result = Instantiate(type, true, ObjectAccessor.Get(arguments));
			if (contracts != null)
				Contracts = oldContracts;
			return result;
		}

		public ContainerService Instantiate(Type type, bool crearteNew, IObjectAccessor arguments)
		{
			var builder = new ContainerService.Builder(type, this, crearteNew, arguments);
			if (builder.Status != ServiceStatus.Ok)
				return builder.Build();
			var declaredName = builder.GetDeclaredName();
			if (!constructingServices.Add(declaredName))
			{
				var previous = GetTopBuilder();
				if (previous == null)
					throw new InvalidOperationException(string.Format("assertion failure, service [{0}]", declaredName));
				var message = string.Format("cyclic dependency {0}{1} -> {0}",
					type.FormatName(), previous.Type == type ? "" : " ...-> " + previous.Type.FormatName());
				var cycleBuilder = new ContainerService.Builder(type, this, false, null);
				cycleBuilder.SetError(message);
				return cycleBuilder.Build();
			}
			stack.Add(builder);
			var expandResult = TryExpandUnions(Container.Configuration);
			if (expandResult != null)
			{
				var poppedContracts = Contracts.PopMany(expandResult.Length);
				foreach (var c in expandResult.CartesianProduct())
				{
					var childService = Resolve(new ServiceName(builder.Type, c));
					builder.LinkTo(childService, null);
					if (builder.Status.IsBad())
						break;
				}
				Contracts.AddRange(poppedContracts);
			}
			else
				Container.Instantiate(builder);
			stack.RemoveLast();
			constructingServices.Remove(declaredName);
			return builder.Build();
		}

		private string[][] TryExpandUnions(ConfigurationRegistry configuration)
		{
			string[][] result = null;
			var startIndex = 0;
			for (var i = 0; i < Contracts.Count; i++)
			{
				var contract = Contracts[i];
				var union = configuration.GetContractsUnionOrNull(contract);
				if (union == null)
				{
					if (result != null)
						result[i - startIndex] = new[] {contract};
				}
				else
				{
					if (result == null)
					{
						startIndex = i;
						result = new string[Contracts.Count - startIndex][];
					}
					result[i - startIndex] = union.ToArray();
				}
			}
			return result;
		}

		public ContainerService.Builder GetTopBuilder()
		{
			return stack.Count == 0 ? null : stack[stack.Count - 1];
		}

		public ContainerService.Builder GetPreviousBuilder()
		{
			return stack.Count <= 1 ? null : stack[stack.Count - 2];
		}

		public ContainerService Resolve(ServiceName name)
		{
			if (name.Contracts.Length == 0)
				return Container.ResolveSingleton(name.Type, this);
			var pushedCount = 0;
			foreach (var newContract in name.Contracts)
			{
				foreach (var c in Contracts)
					if (newContract.EqualsIgnoringCase(c))
					{
						var resultBuilder = new ContainerService.Builder(name.Type, this, false, null);
						const string messageFormat = "contract [{0}] already declared, all declared contracts [{1}]";
						resultBuilder.SetError(string.Format(messageFormat, newContract, InternalHelpers.FormatContractsKey(Contracts)));
						Contracts.RemoveLast(pushedCount);
						return resultBuilder.Build();
					}
				Contracts.Add(newContract);
				pushedCount++;
			}
			var result = Container.ResolveSingleton(name.Type, this);
			Contracts.RemoveLast(name.Contracts.Length);
			return result;
		}
	}
}