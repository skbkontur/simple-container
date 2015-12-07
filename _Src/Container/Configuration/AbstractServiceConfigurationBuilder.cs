using System;
using System.Collections.Generic;
using SimpleContainer.Interface;

namespace SimpleContainer.Configuration
{
	public abstract class AbstractServiceConfigurationBuilder<TSelf, TService>
		where TSelf : AbstractServiceConfigurationBuilder<TSelf, TService>
	{
		internal readonly ServiceConfigurationSet configurationSet;
		protected readonly List<string> contracts;

		protected TSelf Self
		{
			get { return (TSelf) this; }
		}

		internal AbstractServiceConfigurationBuilder(ServiceConfigurationSet configurationSet, List<string> contracts)
		{
			this.configurationSet = configurationSet;
			this.contracts = contracts;
		}

		public TSelf Dependencies(object values)
		{
			GetServiceBuilder().BindDependencies(values);
			return Self;
		}

		public TSelf Dependencies(IParametersSource parameters)
		{
			GetServiceBuilder().BindDependencies(parameters);
			return Self;
		}

		public TSelf BindDependencyImplementation<TDependencyValue>(string dependencyName)
		{
			GetServiceBuilder().BindDependencyImplementation<TDependencyValue>(dependencyName);
			return Self;
		}

		public TSelf BindDependencyImplementation<TDependencyInterface, TDependencyImplementation>()
		{
			GetServiceBuilder().BindDependencyImplementation<TDependencyInterface, TDependencyImplementation>();
			return Self;
		}

		public TSelf BindDependencyValue<TValue>(TValue value)
		{
			GetServiceBuilder().BindDependencyValue(typeof (TValue), value);
			return Self;
		}

		public TSelf BindDependencyValue(Type dependencyType, object value)
		{
			GetServiceBuilder().BindDependencyValue(dependencyType, value);
			return Self;
		}

		public TSelf BindDependencyFactory(string dependencyName, Func<IContainer, object> creator)
		{
			GetServiceBuilder().BindDependencyFactory(dependencyName, creator);
			return Self;
		}

		public TSelf Bind<TImplementation>(bool clearOld = false) where TImplementation : TService
		{
			GetServiceBuilder().Bind(typeof (TService), typeof (TImplementation), clearOld);
			return Self;
		}

		public TSelf Bind(Type type, bool clearOld = false)
		{
			GetServiceBuilder().Bind(typeof(TService), type, clearOld);
			return Self;
		}

		public TSelf Bind(Func<IContainer, TService> factory, bool containerOwnsInstance = true)
		{
			GetServiceBuilder().Bind(c => factory(c), containerOwnsInstance);
			return Self;
		}
		
		public TSelf Bind(Func<IContainer, Type, TService> factory, bool containerOwnsInstance = true)
		{
			GetServiceBuilder().Bind((c, t) => factory(c, t), containerOwnsInstance);
			return Self;
		}

		public TSelf Bind(TService value, bool containerOwnsInstance = true)
		{
			GetServiceBuilder().Bind(typeof (TService), value, containerOwnsInstance);
			return Self;
		}

		public TSelf WithInstanceFilter(Func<TService, bool> filter)
		{
			GetServiceBuilder().WithInstanceFilter(filter);
			return Self;
		}

		public TSelf WithImplicitDependency(ServiceName name)
		{
			GetServiceBuilder().WithImplicitDependency(name);
			return Self;
		}
		
		public TSelf WithComment(string comment)
		{
			GetServiceBuilder().SetComment(comment);
			return Self;
		}

		public TSelf DontUse()
		{
			GetServiceBuilder().DontUse();
			return Self;
		}
		
		public TSelf IgnoreImplementation()
		{
			GetServiceBuilder().IgnoreImplementation();
			return Self;
		}

		private ServiceConfiguration.Builder GetServiceBuilder()
		{
			return configurationSet.GetBuilder(contracts);
		}
	}
}