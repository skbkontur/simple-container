using System;

namespace SimpleContainer.Factories
{
	public abstract class FactoryCreatorBase: IFactoryPlugin
	{
		protected interface IDelegateCaster
		{
			Delegate CastToTyped(Func<object> f);
			Delegate CastToTyped(Func<object, object> f);
			Delegate CastToTyped(Func<Type, object, object> f);
		}

		private class DelegateCaster<T>: IDelegateCaster
		{
			public Delegate CastToTyped(Func<object> f)
			{
				Func<T> result = () => (T) f();
				return result;
			}

			public Delegate CastToTyped(Func<object, object> f)
			{
				Func<object, T> result = o => (T) f(o);
				return result;
			}

			public Delegate CastToTyped(Func<Type, object, object> f)
			{
				Func<Type, object, T> result = (t, o) => (T) f(t, o);
				return result;
			}
		}

		protected static IDelegateCaster GetCaster(Type resultType)
		{
			var casterType = typeof (DelegateCaster<>).MakeGenericType(resultType);
			var caster = (IDelegateCaster) Activator.CreateInstance(casterType);
			return caster;
		}

		public abstract bool TryInstantiate(ContainerService containerService);
	}
}