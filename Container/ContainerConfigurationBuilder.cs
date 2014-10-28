using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Reflection;

namespace SimpleContainer
{
	public class ContainerConfigurationBuilder
	{
		private readonly IDictionary<Type, object> configurations = new Dictionary<Type, object>();

		private bool canCreateChildContainers;
		private readonly List<Action> resetActions = new List<Action>();
		private string hostName;

		private readonly IDictionary<string, ContainerConfigurationBuilder> contractConfigurators =
			new Dictionary<string, ContainerConfigurationBuilder>();

		private readonly List<Action<ContainerConfigurationBuilder, Type>> scanners =
			new List<Action<ContainerConfigurationBuilder, Type>>();

		public ContainerConfigurationBuilder Bind<TInterface, TImplementation>() where TImplementation : TInterface
		{
			return Bind(typeof (TInterface), typeof (TImplementation));
		}

		public ContainerConfigurationBuilder WithHostName(string newHostName)
		{
			hostName = newHostName;
			return this;
		}

		public ContainerConfigurationBuilder Bind(Type interfaceType, Type implementationType)
		{
			if (!interfaceType.IsAssignableFrom(implementationType))
				throw new SimpleContainerException(string.Format("[{0}] is not assignable from [{1}]", interfaceType.FormatName(),
					implementationType.FormatName()));
			GetOrCreate<InterfaceConfiguration>(interfaceType).AddImplementation(implementationType);
			return this;
		}

		public ContainerConfigurationBuilder Bind<T>(object value)
		{
			return Bind(typeof (T), value);
		}

		public ContainerConfigurationBuilder Bind(Type interfaceType, object value)
		{
			if (value != null && interfaceType.IsInstanceOfType(value) == false)
				throw new SimpleContainerException(string.Format("value {0} can't be casted to required type [{1}]",
					SimpleContainerHelpers.DumpValue(value),
					interfaceType.FormatName()));
			GetOrCreate<InterfaceConfiguration>(interfaceType).Implementation = value;
			return this;
		}

		public ContainerConfigurationBuilder Bind<T>(Func<FactoryContext, object> creator)
		{
			GetOrCreate<InterfaceConfiguration>(typeof (T)).Factory = creator;
			return this;
		}

		public void BindDependency<T>(string dependencyName, object value)
		{
			ConfigureDependency(typeof (T), dependencyName).UseValue(value);
		}

		public void BindDependency(Type type, string dependencyName, object value)
		{
			ConfigureDependency(type, dependencyName).UseValue(value);
		}

		public void RequireContract(Type type, string dependencyName, string contract)
		{
			ConfigureDependency(type, dependencyName).AddContract(contract);
		}

		public void RequireContract<T>(string dependencyName, string contract)
		{
			RequireContract(typeof (T), dependencyName, contract);
		}

		public void BindDependency<T, TDependency>(TDependency value)
		{
			BindDependency<T, TDependency>((object) value);
		}

		public void BindDependency<T, TDependency>(object value)
		{
			if (value != null && value is TDependency == false)
				throw new SimpleContainerException(
					string.Format("dependency {0} for service [{1}] can't be casted to required type [{2}]",
						SimpleContainerHelpers.DumpValue(value),
						typeof (T).FormatName(),
						typeof (TDependency).FormatName()));
			ConfigureDependency(typeof (T), typeof (TDependency)).UseValue(value);
		}

		public void BindDependency<T, TDependency, TDependencyValue>() where TDependencyValue : TDependency
		{
			ConfigureDependency(typeof (T), typeof (TDependency)).ImplementationType = typeof (TDependencyValue);
		}

		public void BindDependency(Type type, Type dependencyType, Func<IContainer, object> creator)
		{
			ConfigureDependency(type, dependencyType).Factory = creator;
		}

		public void BindDependencyFactory<T>(string dependencyName, Func<IContainer, object> creator)
		{
			ConfigureDependency(typeof (T), dependencyName).Factory = creator;
		}

		public void BindDependencyImplementation<T, TDependencyValue>(string dependencyName)
		{
			ConfigureDependency(typeof (T), dependencyName).ImplementationType = typeof (TDependencyValue);
		}

		public void BindDependencies<T>(object dependencies)
		{
			throw new NotSupportedException();
		}

		public void BindDependencyValue(Type type, Type dependencyType, object value)
		{
			ConfigureDependency(type, dependencyType).UseValue(value);
		}

		public void ScanTypesWith(Action<ContainerConfigurationBuilder, Type> scanner)
		{
			scanners.Add(scanner);
		}

		public void ApplyScanners(Type[] types)
		{
			foreach (var scanner in scanners)
				foreach (var type in types)
					scanner(this, type);
		}

		public ContainerConfigurationBuilder ConfigureContract(string contract)
		{
			if (contractConfigurators.ContainsKey(contract))
				throw new InvalidOperationException(string.Format("contract {0} already defined", contract));
			var result = new ContainerConfigurationBuilder();
			contractConfigurators[contract] = result;
			return result;
		}

		public ContainerConfigurationBuilder InContext(Type type, params string[] dependencies)
		{
			var targetType = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
			var result = new ContainerConfigurationBuilder();
			foreach (var dependency in dependencies)
			{
				var contract = targetType.Name + "." + dependency;
				RequireContract(type, dependency, contract);
				if (contractConfigurators.ContainsKey(contract))
					throw new InvalidOperationException(string.Format("context key {0} already defined", contract));
				contractConfigurators[contract] = result;
			}
			return result;
		}

		public ContainerConfigurationBuilder InContext<T>(params string[] dependencies)
		{
			return InContext(typeof (T), dependencies);
		}

		public ContainerConfigurationBuilder SetResetAction(Action action)
		{
			resetActions.Add(action);
			return this;
		}

		public ContainerConfigurationBuilder UseAutosearch(Type interfaceType, bool useAutosearch)
		{
			GetOrCreate<InterfaceConfiguration>(interfaceType).UseAutosearch = useAutosearch;
			return this;
		}

		public ContainerConfigurationBuilder DontUsePluggable(Type pluggableType)
		{
			GetOrCreate<ImplementationConfiguration>(pluggableType).DontUseIt = true;
			return this;
		}

		public ContainerConfigurationBuilder DontUsePluggable<T>()
		{
			return DontUsePluggable(typeof (T));
		}

		public ContainerConfigurationBuilder EnableChildContainerCreation()
		{
			canCreateChildContainers = true;
			return this;
		}

		public IContainerConfiguration Build()
		{
			return new ContainerConfiguration(configurations, canCreateChildContainers,
				delegate
				{
					foreach (var resetAction in resetActions)
						resetAction();
				},
				contractConfigurators.ToDictionary(x => x.Key, x => x.Value.Build()),
				hostName);
		}

		private ImplentationDependencyConfiguration ConfigureDependency(Type implementationType, Type dependencyType)
		{
			return GetDependencyConfigurator(implementationType, dependencyType.FormatName() + " type");
		}

		private ImplentationDependencyConfiguration ConfigureDependency(Type implementationType, string dependencyName)
		{
			return GetDependencyConfigurator(implementationType, dependencyName + " name");
		}

		private ImplentationDependencyConfiguration GetDependencyConfigurator(Type pluggable, string key)
		{
			return GetOrCreate<ImplementationConfiguration>(pluggable).GetOrCreateByKey(key);
		}

		private T GetOrCreate<T>(Type type) where T : class, new()
		{
			object result;
			if (!configurations.TryGetValue(type, out result))
				configurations.Add(type, result = new T());
			try
			{
				return (T) result;
			}
			catch (InvalidCastException e)
			{
				throw new InvalidOperationException(string.Format("type {0}, existent {1}, required {2}",
					type.FormatName(), result.GetType().FormatName(), typeof (T).FormatName()), e);
			}
		}

		private class ContainerConfiguration : IContainerConfiguration
		{
			private readonly IDictionary<Type, object> configurations;
			private readonly IDictionary<string, IContainerConfiguration> contractsConfigurators;

			public ContainerConfiguration(IDictionary<Type, object> configurations, bool canCreateChildContainers,
				Action resetAction,
				IDictionary<string, IContainerConfiguration> contractsConfigurators, string hostName)
			{
				this.configurations = configurations;
				this.contractsConfigurators = contractsConfigurators;
				CanCreateChildContainers = canCreateChildContainers;
				ResetAction = resetAction;
				HostName = hostName;
			}

			public bool CanCreateChildContainers { get; private set; }
			public Action ResetAction { get; private set; }

			public T GetOrNull<T>(Type type) where T : class
			{
				return configurations.GetOrDefault(type) as T;
			}

			public IContainerConfiguration GetByKeyOrNull(string contextKey)
			{
				return contractsConfigurators.GetOrDefault(contextKey);
			}

			public string HostName { get; private set; }
		}
	}
}