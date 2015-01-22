using System;
using System.Collections.Generic;
using System.Reflection;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;
using SimpleContainer.Interface;

namespace SimpleContainer.Configuration
{
	public abstract class AbstractConfigurationBuilder<TSelf> : IInternalConfigurationBuilder
		where TSelf : AbstractConfigurationBuilder<TSelf>
	{
		protected readonly bool isStaticConfiguration;
		protected readonly ISet<Type> staticServices;
		protected readonly IDictionary<Type, object> configurations = new Dictionary<Type, object>();

		protected AbstractConfigurationBuilder(ISet<Type> staticServices, bool isStaticConfiguration)
		{
			this.staticServices = staticServices;
			this.isStaticConfiguration = isStaticConfiguration;
		}

		protected TSelf Self
		{
			get { return (TSelf) this; }
		}

		public TSelf Bind<TInterface, TImplementation>(bool clearOld = false)
			where TImplementation : TInterface
		{
			return Bind(typeof (TInterface), typeof (TImplementation), clearOld);
		}

		public TSelf Bind(Type interfaceType, Type implementationType, bool clearOld)
		{
			if (!interfaceType.IsAssignableFrom(implementationType))
				throw new SimpleContainerException(string.Format("[{0}] is not assignable from [{1}]", interfaceType.FormatName(),
					implementationType.FormatName()));
			GetOrCreate<InterfaceConfiguration>(interfaceType).AddImplementation(implementationType, clearOld);
			return Self;
		}

		public TSelf Bind(Type interfaceType, Type implementationType)
		{
			return Bind(interfaceType, implementationType, false);
		}

		public TSelf Bind<T>(object value)
		{
			return Bind(typeof (T), value);
		}

		public TSelf Bind(Type interfaceType, object value)
		{
			if (value != null && interfaceType.IsInstanceOfType(value) == false)
				throw new SimpleContainerException(string.Format("value {0} can't be casted to required type [{1}]",
					DumpValue(value),
					interfaceType.FormatName()));
			GetOrCreate<InterfaceConfiguration>(interfaceType).UseInstance(value);
			return Self;
		}

		public TSelf WithInstanceFilter<T>(Func<T, bool> filter)
		{
			GetOrCreate<ImplementationConfiguration>(typeof (T)).InstanceFilter = o => filter((T) o);
			return Self;
		}

		public TSelf Bind<T>(Func<FactoryContext, T> creator)
		{
			return Bind(typeof (T), c => creator(c));
		}
		
		public TSelf Bind(Type type, Func<FactoryContext, object> creator)
		{
			GetOrCreate<InterfaceConfiguration>(type).Factory = creator;
			return Self;
		}

		public TSelf BindDependency<T>(string dependencyName, object value)
		{
			ConfigureDependency(typeof (T), dependencyName).UseValue(value);
			return Self;
		}

		public TSelf BindDependency(Type type, string dependencyName, object value)
		{
			ConfigureDependency(type, dependencyName).UseValue(value);
			return Self;
		}

		public TSelf BindDependency<T, TDependency>(TDependency value)
		{
			BindDependency<T, TDependency>((object) value);
			return Self;
		}

		public TSelf BindDependency<T, TDependency>(object value)
		{
			if (value != null && value is TDependency == false)
				throw new SimpleContainerException(
					string.Format("dependency {0} for service [{1}] can't be casted to required type [{2}]",
						DumpValue(value),
						typeof (T).FormatName(),
						typeof (TDependency).FormatName()));
			ConfigureDependency(typeof (T), typeof (TDependency)).UseValue(value);
			return Self;
		}

		public TSelf BindDependency<T, TDependency, TDependencyValue>()
			where TDependencyValue : TDependency
		{
			ConfigureDependency(typeof (T), typeof (TDependency)).ImplementationType = typeof (TDependencyValue);
			return Self;
		}

		public TSelf BindDependency(Type type, Type dependencyType, Func<IContainer, object> creator)
		{
			ConfigureDependency(type, dependencyType).Factory = creator;
			return Self;
		}

		public TSelf BindDependencyFactory<T>(string dependencyName, Func<IContainer, object> creator)
		{
			ConfigureDependency(typeof (T), dependencyName).Factory = creator;
			return Self;
		}

		public TSelf BindDependencyImplementation<T, TDependencyValue>(string dependencyName)
		{
			ConfigureDependency(typeof (T), dependencyName).ImplementationType = typeof (TDependencyValue);
			return Self;
		}

		public TSelf BindDependencies<T>(object dependencies)
		{
			foreach (var property in dependencies.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
				BindDependency<T>(property.Name, property.GetValue(dependencies, null));
			return Self;
		}

		public TSelf BindDependencies<T>(IParametersSource parameters)
		{
			GetOrCreate<ImplementationConfiguration>(typeof (T)).ParametersSource = parameters;
			return Self;
		}

		public TSelf BindDependencyValue(Type type, Type dependencyType, object value)
		{
			ConfigureDependency(type, dependencyType).UseValue(value);
			return Self;
		}

		public TSelf UseAutosearch(Type interfaceType, bool useAutosearch)
		{
			GetOrCreate<InterfaceConfiguration>(interfaceType).UseAutosearch = useAutosearch;
			return Self;
		}

		public TSelf DontUse(Type pluggableType)
		{
			GetOrCreate<ImplementationConfiguration>(pluggableType).DontUseIt = true;
			return Self;
		}

		public TSelf DontUse<T>()
		{
			return DontUse(typeof (T));
		}

		void IInternalConfigurationBuilder.DontUse(Type pluggableType)
		{
			DontUse(pluggableType);
		}

		void IInternalConfigurationBuilder.Bind(Type interfaceType, Type implementationType)
		{
			Bind(interfaceType, implementationType);
		}

		void IInternalConfigurationBuilder.BindDependency(Type type, string dependencyName, object value)
		{
			BindDependency(type, dependencyName, value);
		}

		private ImplentationDependencyConfiguration ConfigureDependency(Type implementationType, Type dependencyType)
		{
			return GetDependencyConfigurator(implementationType, InternalHelpers.ByTypeDependencyKey(dependencyType));
		}

		private ImplentationDependencyConfiguration ConfigureDependency(Type implementationType, string dependencyName)
		{
			return GetDependencyConfigurator(implementationType, InternalHelpers.ByNameDependencyKey(dependencyName));
		}

		private ImplentationDependencyConfiguration GetDependencyConfigurator(Type pluggable, string key)
		{
			return GetOrCreate<ImplementationConfiguration>(pluggable).GetOrCreateByKey(key);
		}

		protected T GetOrCreate<T>(Type type) where T : class, new()
		{
			var isStatic = staticServices.Contains(type) || type.IsDefined<StaticAttribute>();
			if (isStatic && !isStaticConfiguration)
			{
				const string messageFormat = "can't configure static service [{0}] using non static configurator";
				throw new SimpleContainerException(string.Format(messageFormat, type.FormatName()));
			}
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

		private static string DumpValue(object value)
		{
			if (value == null)
				return "[<null>]";
			var type = value.GetType();
			return ReflectionHelpers.simpleTypes.Contains(type)
				? string.Format("[{0}] of type [{1}]", value, type.FormatName())
				: string.Format("of type [{0}]", type.FormatName());
		}
	}
}