using System;
using System.Collections.Generic;
using SimpleContainer.Interface;

namespace SimpleContainer.Configuration
{
	public abstract class AbstractConfigurationBuilder<TSelf>
		where TSelf : AbstractConfigurationBuilder<TSelf>
	{
		internal ConfigurationRegistry.Builder RegistryBuilder { get; private set; }
		protected readonly List<string> contracts;

		internal AbstractConfigurationBuilder(ConfigurationRegistry.Builder registryBuilder, List<string> contracts)
		{
			RegistryBuilder = registryBuilder;
			this.contracts = contracts;
		}

		public TSelf Bind<TInterface, TImplementation>(bool clearOld = false)
			where TImplementation : TInterface
		{
			GetServiceBuilder(typeof (TInterface)).Bind(typeof (TInterface), typeof (TImplementation), clearOld);
			return Self;
		}

		public TSelf Bind(Type interfaceType, Type implementationType, bool clearOld)
		{
			GetServiceBuilder(interfaceType).Bind(interfaceType, implementationType, clearOld);
			return Self;
		}

		public TSelf Bind(Type interfaceType, Type implementationType)
		{
			GetServiceBuilder(interfaceType).Bind(interfaceType, implementationType, false);
			return Self;
		}

		public TSelf Bind<T>(object value, bool containerOwnsInstance = true)
		{
			GetServiceBuilder(typeof (T)).Bind(typeof (T), value, containerOwnsInstance);
			return Self;
		}

		public TSelf Bind(Type interfaceType, object value, bool containerOwnsInstance = true)
		{
			GetServiceBuilder(interfaceType).Bind(interfaceType, value, containerOwnsInstance);
			return Self;
		}

		public TSelf WithInstanceFilter<T>(Func<T, bool> filter)
		{
			GetServiceBuilder(typeof (T)).WithInstanceFilter(filter);
			return Self;
		}

		public TSelf Bind<T>(Func<FactoryContext, T> creator, bool containerOwnsInstance = true)
		{
			GetServiceBuilder(typeof (T)).Bind(creator, containerOwnsInstance);
			return Self;
		}

		public TSelf Bind(Type type, Func<FactoryContext, object> creator, bool containerOwnsInstance = true)
		{
			GetServiceBuilder(type).Bind(creator, containerOwnsInstance);
			return Self;
		}

		public TSelf BindDependency<T>(string dependencyName, object value)
		{
			GetServiceBuilder(typeof (T)).BindDependency(dependencyName, value);
			return Self;
		}

		public TSelf BindDependency(Type type, string dependencyName, object value)
		{
			GetServiceBuilder(type).BindDependency(dependencyName, value);
			return Self;
		}

		public TSelf BindDependency<T, TDependency>(TDependency value)
		{
			GetServiceBuilder(typeof (T)).BindDependency<T, TDependency>(value);
			return Self;
		}

		public TSelf BindDependency<T, TDependency>(object value)
		{
			GetServiceBuilder(typeof (T)).BindDependency<T, TDependency>(value);
			return Self;
		}

		public TSelf BindDependency<T, TDependency, TDependencyValue>()
			where TDependencyValue : TDependency
		{
			GetServiceBuilder(typeof (T)).BindDependency<TDependency, TDependencyValue>();
			return Self;
		}

		public TSelf BindDependency(Type type, Type dependencyType, Func<IContainer, object> creator)
		{
			GetServiceBuilder(type).BindDependency(dependencyType, creator);
			return Self;
		}

		public TSelf BindDependencyFactory<T>(string dependencyName, Func<IContainer, object> creator)
		{
			GetServiceBuilder(typeof (T)).BindDependencyFactory(dependencyName, creator);
			return Self;
		}

		public TSelf BindDependencyImplementation<T, TDependencyValue>(string dependencyName)
		{
			GetServiceBuilder(typeof (T)).BindDependencyImplementation<TDependencyValue>(dependencyName);
			return Self;
		}

		public TSelf BindDependencyImplementation<T, TDependencyInterface, TDependencyImplementation>()
		{
			GetServiceBuilder(typeof (T)).BindDependencyImplementation<TDependencyInterface, TDependencyImplementation>();
			return Self;
		}

		public TSelf BindDependencies<T>(object dependencies)
		{
			GetServiceBuilder(typeof (T)).BindDependencies(dependencies);
			return Self;
		}

		public TSelf BindDependencies<T>(IParametersSource parameters)
		{
			GetServiceBuilder(typeof (T)).BindDependencies(parameters);
			return Self;
		}

		public TSelf BindDependencyValue(Type type, Type dependencyType, object value)
		{
			GetServiceBuilder(type).BindDependencyValue(dependencyType, value);
			return Self;
		}

		public TSelf UseAutosearch(Type interfaceType, bool useAutosearch)
		{
			GetServiceBuilder(interfaceType).UseAutosearch(useAutosearch);
			return Self;
		}

		public TSelf DontUse(Type pluggableType)
		{
			GetServiceBuilder(pluggableType).DontUse();
			return Self;
		}

		public TSelf DontUse<T>()
		{
			GetServiceBuilder(typeof (T)).DontUse();
			return Self;
		}

		private ServiceConfiguration.Builder GetServiceBuilder(Type type)
		{
			return RegistryBuilder.GetConfigurationSet(type).GetBuilder(contracts);
		}

		protected TSelf Self
		{
			get { return (TSelf) this; }
		}
	}
}