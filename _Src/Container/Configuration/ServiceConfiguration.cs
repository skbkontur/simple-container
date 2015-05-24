using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Configuration
{
	internal class ServiceConfiguration
	{
		private ServiceConfiguration(List<string> contracts)
		{
			Contracts = contracts;
		}

		private ImplentationDependencyConfiguration[] dependencies;
		public List<string> Contracts { get; private set; }
		public Type[] ImplementationTypes { get; private set; }
		public object Implementation { get; private set; }
		public bool ImplementationAssigned { get; private set; }
		public Func<FactoryContext, object> Factory { get; set; }
		public bool UseAutosearch { get; private set; }
		public bool ContainerOwnsInstance { get; private set; }
		public bool DontUseIt { get; private set; }
		public Func<object, bool> InstanceFilter { get; private set; }
		public IParametersSource ParametersSource { get; private set; }

		public ServiceConfiguration CloneWithFilter(Func<Type, bool> filter)
		{
			return new ServiceConfiguration(Contracts)
			{
				Factory = Factory,
				Implementation = Implementation,
				ImplementationAssigned = ImplementationAssigned,
				ImplementationTypes = ImplementationTypes.Where(filter).ToArray(),
				UseAutosearch = UseAutosearch,
				ContainerOwnsInstance = ContainerOwnsInstance,
				DontUseIt = DontUseIt,
				InstanceFilter = InstanceFilter,
				ParametersSource = ParametersSource,
				dependencies = dependencies
			};
		}

		internal static readonly ServiceConfiguration empty = new ServiceConfiguration(new List<string>())
		{
			ContainerOwnsInstance = true,
			dependencies = new ImplentationDependencyConfiguration[0]
		};

		public ImplentationDependencyConfiguration GetByKeyOrNull(string key)
		{
			return dependencies.SingleOrDefault(x => x.Key == key);
		}

		public ImplentationDependencyConfiguration GetOrNull(ParameterInfo parameter)
		{
			var result = GetByKeyOrNull(InternalHelpers.ByNameDependencyKey(parameter.Name)) ??
			             GetByKeyOrNull(InternalHelpers.ByTypeDependencyKey(parameter.ParameterType));
			if (result != null)
				result.Used = true;
			return result;
		}

		public string[] GetUnusedDependencyConfigurationKeys()
		{
			return dependencies.Where(x => !x.Used).Select(x => x.Key).ToArray();
		}

		internal class Builder
		{
			private readonly ServiceConfiguration target;
			public List<Type> ImplementationTypes { get; private set; }

			private readonly List<ImplentationDependencyConfiguration.Builder> dependencyBuilders =
				new List<ImplentationDependencyConfiguration.Builder>();

			public Builder(List<string> contracts)
			{
				target = new ServiceConfiguration(contracts);
			}

			public List<string> Contracts
			{
				get { return target.Contracts; }
			}

			public void Bind(Type interfaceType, Type implementationType, bool clearOld)
			{
				if (!interfaceType.IsGenericTypeDefinition && !implementationType.IsGenericTypeDefinition &&
				    !interfaceType.IsAssignableFrom(implementationType))
					throw new SimpleContainerException(string.Format("[{0}] is not assignable from [{1}]", interfaceType.FormatName(),
						implementationType.FormatName()));
				AddImplementation(implementationType, clearOld);
			}

			public void Bind(Type interfaceType, object value, bool containerOwnsInstance)
			{
				if (interfaceType.ContainsGenericParameters)
					throw new SimpleContainerException(string.Format("can't bind value for generic definition [{0}]",
						interfaceType.FormatName()));
				if (value != null && interfaceType.IsInstanceOfType(value) == false)
					throw new SimpleContainerException(string.Format("value {0} can't be casted to required type [{1}]",
						DumpValue(value),
						interfaceType.FormatName()));
				UseInstance(value, containerOwnsInstance);
			}

			public void WithInstanceFilter<T>(Func<T, bool> filter)
			{
				target.InstanceFilter = o => filter((T) o);
			}

			public void Bind(Func<FactoryContext, object> creator, bool containerOwnsInstance)
			{
				target.Factory = creator;
				target.ContainerOwnsInstance = containerOwnsInstance;
			}

			public void Bind<T>(Func<FactoryContext, T> creator, bool containerOwnsInstance)
			{
				target.Factory = c => creator(c);
				target.ContainerOwnsInstance = containerOwnsInstance;
			}

			public void DontUse()
			{
				target.DontUseIt = true;
			}

			public void UseAutosearch(bool useAutosearch)
			{
				target.UseAutosearch = useAutosearch;
			}

			public void BindDependencies(IParametersSource parameters)
			{
				target.ParametersSource = parameters;
			}

			public void BindDependencies(object dependencies)
			{
				foreach (var property in dependencies.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
					BindDependency(property.Name, property.GetValue(dependencies, null));
			}

			public void BindDependency(string dependencyName, object value)
			{
				GetDependencyBuilderByName(dependencyName).UseValue(value);
			}

			public void BindDependencyValue(Type dependencyType, object value)
			{
				GetDependencyBuilderByType(dependencyType).UseValue(value);
			}

			public void BindDependency(Type dependencyType, Func<IContainer, object> creator)
			{
				GetDependencyBuilderByType(dependencyType).UseFactory(creator);
			}

			public void BindDependency<T, TDependency>(object value)
			{
				if (value != null && value is TDependency == false)
					throw new SimpleContainerException(
						string.Format("dependency {0} for service [{1}] can't be casted to required type [{2}]",
							DumpValue(value),
							typeof (T).FormatName(),
							typeof (TDependency).FormatName()));
				GetDependencyBuilderByType(typeof (TDependency)).UseValue(value);
			}

			public void BindDependency<TDependency, TDependencyValue>()
				where TDependencyValue : TDependency
			{
				GetDependencyBuilderByType(typeof (TDependency)).UseImplementation(typeof (TDependencyValue));
			}

			public void BindDependencyFactory(string dependencyName, Func<IContainer, object> creator)
			{
				GetDependencyBuilderByName(dependencyName).UseFactory(creator);
			}

			public void BindDependencyImplementation<TDependencyValue>(string dependencyName)
			{
				GetDependencyBuilderByName(dependencyName).UseImplementation(typeof (TDependencyValue));
			}

			public void BindDependencyImplementation<TDependencyInterface, TDependencyImplementation>()
			{
				GetDependencyBuilderByType(typeof (TDependencyInterface)).UseImplementation(typeof (TDependencyImplementation));
			}

			public ServiceConfiguration Build()
			{
				target.dependencies = dependencyBuilders.Select(x => x.Build()).ToArray();
				if (ImplementationTypes != null)
					target.ImplementationTypes = ImplementationTypes.ToArray();
				return target;
			}

			private void AddImplementation(Type type, bool clearOld)
			{
				if (ImplementationTypes == null)
					ImplementationTypes = new List<Type>();
				if (clearOld)
					ImplementationTypes.Clear();
				if (!ImplementationTypes.Contains(type))
					ImplementationTypes.Add(type);
			}

			private void UseInstance(object instance, bool containerOwnsInstance)
			{
				target.Factory = null;
				target.Implementation = instance;
				target.ImplementationAssigned = true;
				target.ContainerOwnsInstance = containerOwnsInstance;
			}

			private ImplentationDependencyConfiguration.Builder GetDependencyBuilder(string key)
			{
				var result = dependencyBuilders.SingleOrDefault(x => x.Key == key);
				if (result == null)
					dependencyBuilders.Add(result = new ImplentationDependencyConfiguration.Builder(key));
				return result;
			}

			private ImplentationDependencyConfiguration.Builder GetDependencyBuilderByType(Type dependencyType)
			{
				return GetDependencyBuilder(InternalHelpers.ByTypeDependencyKey(dependencyType));
			}

			private ImplentationDependencyConfiguration.Builder GetDependencyBuilderByName(string dependencyName)
			{
				return GetDependencyBuilder(InternalHelpers.ByNameDependencyKey(dependencyName));
			}

			private static string DumpValue(object value)
			{
				if (value == null)
					return "[<null>]";
				var type = value.GetType();
				return type.IsSimpleType()
					? string.Format("[{0}] of type [{1}]", value, type.FormatName())
					: string.Format("of type [{0}]", type.FormatName());
			}
		}
	}
}