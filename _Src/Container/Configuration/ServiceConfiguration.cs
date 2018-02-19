using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation;
using SimpleContainer.Interface;

namespace SimpleContainer.Configuration
{
	internal class ServiceConfiguration
	{
		private ServiceConfiguration(List<string> contracts)
		{
			Contracts = contracts;
		}

		private Dictionary<DependencyKey, ImplentationDependencyConfiguration> dependencies;
		public List<string> Contracts { get; private set; }
		public List<Type> ImplementationTypes { get; private set; }
		public object Implementation { get; private set; }
		public bool ImplementationAssigned { get; private set; }
		public bool FactoryDependsOnTarget { get; private set; }
		public Func<ContainerService.Builder, object> Factory { get; private set; }
		public bool ContainerOwnsInstance { get; private set; }
		public bool DontUseIt { get; private set; }
		public bool IgnoredImplementation { get; private set; }
		public Func<object, bool> InstanceFilter { get; private set; }
		public IParametersSource ParametersSource { get; private set; }
		public string Comment { get; private set; }
		public ServiceName[] ImplicitDependencies { get; private set; }

		internal static readonly ServiceConfiguration empty = new ServiceConfiguration(new List<string>())
		{
			ContainerOwnsInstance = true,
			dependencies = new Dictionary<DependencyKey, ImplentationDependencyConfiguration>(),
			ImplicitDependencies = InternalHelpers.emptyServiceNames
		};

		public ImplentationDependencyConfiguration GetByKeyOrNull(DependencyKey key)
		{
			ImplentationDependencyConfiguration result;
			return dependencies.TryGetValue(key, out result) ? result : null;
		}

		public ImplentationDependencyConfiguration GetOrNull(ParameterInfo parameter)
		{
			var result = GetByKeyOrNull(new DependencyKey(parameter.Name)) ??
			             GetByKeyOrNull(new DependencyKey(parameter.ParameterType));
			if (result != null)
				result.Used = true;
			return result;
		}

		public List<DependencyKey> GetUnusedDependencyKeys()
		{
			var result = new List<DependencyKey>();
			foreach (var d in dependencies)
			{
				if (!d.Value.Used)
					result.Add(d.Key);
			}
			return result;
		}

		internal class Builder
		{
			private readonly ServiceConfiguration target;
			public List<ServiceName> ImplicitDependencies { get; private set; }

			private readonly Dictionary<DependencyKey, ImplentationDependencyConfiguration.Builder> dependencyBuilders =
				new Dictionary<DependencyKey, ImplentationDependencyConfiguration.Builder>();

			public Builder(List<string> contracts)
			{
				target = new ServiceConfiguration(contracts);
			}

			public List<string> Contracts
			{
				get { return target.Contracts; }
			}

			public string Comment
			{
				get { return target.Comment; }
			}

			public void Bind(Type interfaceType, Type implementationType, bool clearOld)
			{
				if (!interfaceType.IsGenericTypeDefinition() && !implementationType.IsGenericTypeDefinition() &&
				    !interfaceType.IsAssignableFrom(implementationType))
					throw new SimpleContainerException(string.Format("[{0}] is not assignable from [{1}]",
						interfaceType.FormatName(), implementationType.FormatName()));
				if (target.ImplementationTypes == null)
					target.ImplementationTypes = new List<Type>();
			    target.Factory = null;
			    target.Implementation = null;
			    target.ImplementationAssigned = false;
			    target.ContainerOwnsInstance = false;
				if (clearOld)
					target.ImplementationTypes.Clear();
				if (!target.ImplementationTypes.Contains(implementationType))
					target.ImplementationTypes.Add(implementationType);
			}

			public void WithImplicitDependency(ServiceName name)
			{
				if (ImplicitDependencies == null)
					ImplicitDependencies = new List<ServiceName>();
				ImplicitDependencies.Add(name);
			}

			public void Bind(Type interfaceType, object value, bool containerOwnsInstance)
			{
				if (interfaceType.ContainsGenericParameters())
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

			public void SetComment(string comment)
			{
				target.Comment = comment;
			}

			public void Bind(Func<IContainer, object> creator, bool containerOwnsInstance)
			{
				target.Factory = b => creator(b.Context.Container);
				target.ContainerOwnsInstance = containerOwnsInstance;
			}

			public void Bind(Func<IContainer, Type, object> creator, bool containerOwnsInstance)
			{
				target.Factory = delegate(ContainerService.Builder b)
				{
					var stack = b.Context.Stack;
					var previousService = stack.Count <= 1 ? null : stack[stack.Count - 2];
					var targetType = previousService == null ? null : previousService.Type;
					return creator(b.Context.Container, targetType);
				};
				target.FactoryDependsOnTarget = true;
				target.ContainerOwnsInstance = containerOwnsInstance;
			}

			public void DontUse()
			{
				target.DontUseIt = true;
			}

			public void IgnoreImplementation()
			{
				target.IgnoredImplementation = true;
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
				GetDependencyBuilder(new DependencyKey(dependencyName)).UseValue(value);
			}

			public void BindDependencyValue(Type dependencyType, object value)
			{
				GetDependencyBuilder(new DependencyKey(dependencyType)).UseValue(value);
			}

			public void BindDependency(Type dependencyType, Func<IContainer, object> creator)
			{
				GetDependencyBuilder(new DependencyKey(dependencyType)).UseFactory(creator);
			}

			public void BindDependency<T, TDependency>(object value)
			{
				if (value != null && value is TDependency == false)
					throw new SimpleContainerException(
						string.Format("dependency {0} for service [{1}] can't be casted to required type [{2}]",
							DumpValue(value),
							typeof (T).FormatName(),
							typeof (TDependency).FormatName()));
				GetDependencyBuilder(new DependencyKey(typeof (TDependency))).UseValue(value);
			}

			public void BindDependency<TDependency, TDependencyValue>()
				where TDependencyValue : TDependency
			{
				GetDependencyBuilder(new DependencyKey(typeof (TDependency))).UseImplementation(typeof (TDependencyValue));
			}

			public void BindDependencyFactory(string dependencyName, Func<IContainer, object> creator)
			{
				GetDependencyBuilder(new DependencyKey(dependencyName)).UseFactory(creator);
			}

			public void BindDependencyImplementation<TDependencyValue>(string dependencyName)
			{
				GetDependencyBuilder(new DependencyKey(dependencyName)).UseImplementation(typeof (TDependencyValue));
			}

			public void BindDependencyImplementation<TDependencyInterface, TDependencyImplementation>()
			{
				GetDependencyBuilder(new DependencyKey(typeof (TDependencyInterface)))
					.UseImplementation(typeof (TDependencyImplementation));
			}

			public ServiceConfiguration Build()
			{
				target.dependencies = dependencyBuilders.ToDictionary(x => x.Key, x => x.Value.Build());
				target.ImplicitDependencies = ImplicitDependencies == null
					? InternalHelpers.emptyServiceNames
					: ImplicitDependencies.ToArray();
				return target;
			}

			private void UseInstance(object instance, bool containerOwnsInstance)
			{
				target.Factory = null;
				target.Implementation = instance;
				target.ImplementationAssigned = true;
				target.ContainerOwnsInstance = containerOwnsInstance;
			}

			private ImplentationDependencyConfiguration.Builder GetDependencyBuilder(DependencyKey key)
			{
				ImplentationDependencyConfiguration.Builder builder;
				if (!dependencyBuilders.TryGetValue(key, out builder))
					dependencyBuilders.Add(key, builder = new ImplentationDependencyConfiguration.Builder());
				return builder;
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