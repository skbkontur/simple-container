using System;
using System.Linq;
using System.Reflection;
using SimpleContainer.Helpers;

namespace SimpleContainer.Factories
{
	internal static class DelegateCaster
	{
		public static IDelegateCaster Create(Type resultType)
		{
			var casterType = typeof (DelegateCasterImpl<>).MakeGenericType(resultType);
			return (IDelegateCaster)MethodInvoker.Invoke(casterType.GetTypeInfo().DeclaredConstructors.Single(x => x.GetParameters().Length == 0), null, new object[0]);
		}

		public interface IDelegateCaster
		{
			Delegate Cast(Func<object> f);
			Delegate Cast(Func<object, object> f);
			Delegate Cast(Func<Type, object, object> f);
		}

		private class DelegateCasterImpl<T> : IDelegateCaster
		{
			public Delegate Cast(Func<object> f)
			{
				Func<T> result = () => (T) f();
				return result;
			}

			public Delegate Cast(Func<object, object> f)
			{
				Func<object, T> result = o => (T) f(o);
				return result;
			}

			public Delegate Cast(Func<Type, object, object> f)
			{
				Func<Type, object, T> result = (t, o) => (T) f(t, o);
				return result;
			}
		}
	}
}