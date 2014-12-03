using System;
using SimpleContainer.Implementation;
using SimpleContainer.Infection;

namespace SimpleContainer.Configuration
{
	public class ServiceConfigurationBuilder<T>
	{
		private readonly ContainerConfigurationBuilder builder;

		public ServiceConfigurationBuilder(ContainerConfigurationBuilder builder)
		{
			this.builder = builder;
		}

		public ServiceConfigurationBuilder<T> DontUse()
		{
			builder.DontUse<T>();
			return this;
		}

		public ServiceConfigurationBuilder<T> Contract<TContract>() where TContract : RequireContractAttribute, new()
		{
			return new ServiceConfigurationBuilder<T>(builder.Contract<TContract>());
		}

		public ServiceConfigurationBuilder<T> Contract(string contractName)
		{
			return new ServiceConfigurationBuilder<T>(builder.Contract(contractName));
		}

		public ServiceConfigurationBuilder<T> Dependencies(object values)
		{
			builder.BindDependencies<T>(values);
			return this;
		}

		public ServiceConfigurationBuilder<T> Bind<TImplementation>(bool clearOld = false) where TImplementation : T
		{
			builder.Bind<T, TImplementation>(clearOld);
			return this;
		}

		public ServiceConfigurationBuilder<T> Bind(Type type, bool clearOld = false)
		{
			builder.Bind(typeof (T), type, clearOld);
			return this;
		}

		public ServiceConfigurationBuilder<T> Bind(Func<FactoryContext, T> factory)
		{
			builder.Bind<T>(factory);
			return this;
		}

		public ServiceConfigurationBuilder<T> WithInstanceFilter(Func<T, bool> filter)
		{
			builder.WithInstanceFilter(filter);
			return this;
		}

		public ServiceConfigurationBuilder<T> MakeStatic()
		{
			builder.MakeStatic(typeof (T));
			return this;
		}
	}
}