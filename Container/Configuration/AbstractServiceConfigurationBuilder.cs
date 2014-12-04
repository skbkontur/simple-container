using System;
using SimpleContainer.Implementation;

namespace SimpleContainer.Configuration
{
	public abstract class AbstractServiceConfigurationBuilder<TSelf, TBuilder, TService>
		where TSelf : AbstractServiceConfigurationBuilder<TSelf, TBuilder, TService>
		where TBuilder : AbstractConfigurationBuilder<TBuilder>
	{
		protected readonly TBuilder builder;

		protected TSelf Self
		{
			get { return (TSelf) this; }
		}

		protected AbstractServiceConfigurationBuilder(TBuilder builder)
		{
			this.builder = builder;
		}

		public TSelf Dependencies(object values)
		{
			builder.BindDependencies<TService>(values);
			return Self;
		}

		public TSelf Bind<TImplementation>(bool clearOld = false) where TImplementation : TService
		{
			builder.Bind<TService, TImplementation>(clearOld);
			return Self;
		}

		public TSelf Bind(Type type, bool clearOld = false)
		{
			builder.Bind(typeof (TService), type, clearOld);
			return Self;
		}

		public TSelf Bind(Func<FactoryContext, TService> factory)
		{
			builder.Bind(factory);
			return Self;
		}

		public TSelf WithInstanceFilter(Func<TService, bool> filter)
		{
			builder.WithInstanceFilter(filter);
			return Self;
		}

		public TSelf DontUse()
		{
			builder.DontUse<TService>();
			return Self;
		}
	}
}