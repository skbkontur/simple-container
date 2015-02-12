using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class Implementation
	{
		public readonly ConstructorInfo[] publicConstructors;
		public readonly Type type;
		private ImplementationConfiguration configuration;
		public IObjectAccessor Arguments { get; private set; }

		public Implementation(Type type)
		{
			this.type = type;
			publicConstructors = type.GetConstructors().Where(x => x.IsPublic).ToArray();
		}

		public bool TryGetConstructor(out ConstructorInfo constructor)
		{
			return publicConstructors.SafeTrySingle(out constructor) ||
			       publicConstructors.SafeTrySingle(c => c.IsDefined("ContainerConstructorAttribute"), out constructor);
		}

		public void SetConfiguration(IContainerConfiguration containerConfiguration)
		{
			configuration = containerConfiguration.GetConfiguration<ImplementationConfiguration>(type);
		}

		public void SetService(ContainerService containerService)
		{
			configuration = containerService.Context.GetConfiguration<ImplementationConfiguration>(type);
			Arguments = containerService.Arguments;
		}

		public ImplentationDependencyConfiguration GetDependencyConfiguration(ParameterInfo formalParameter)
		{
			return configuration == null ? null : configuration.GetOrNull(formalParameter);
		}

		public IParametersSource GetParameters()
		{
			return configuration == null ? null : configuration.ParametersSource;
		}

		public IEnumerable<string> GetUnusedDependencyConfigurationNames()
		{
			return configuration != null
				? configuration.GetUnusedDependencyConfigurationKeys()
				: Enumerable.Empty<string>();
		}
	}
}