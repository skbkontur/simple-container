using System;
using System.Collections.Generic;

namespace SimpleContainer.Configuration
{
	internal class EmptyConfigurationRegistry : IConfigurationRegistry
	{
		private readonly ImplementationSelector[] emptyImplementationSelectors = new ImplementationSelector[0];
		private static readonly IConfigurationRegistry instance = new EmptyConfigurationRegistry();

		private EmptyConfigurationRegistry()
		{
		}

		public static IConfigurationRegistry Instance
		{
			get { return instance; }
		}

		public ServiceConfiguration GetConfigurationOrNull(Type type, List<string> contracts)
		{
			return null;
		}

		public List<string> GetContractsUnionOrNull(string contract)
		{
			return null;
		}

		public ImplementationSelector[] GetImplementationSelectors()
		{
			return emptyImplementationSelectors;
		}
	}
}