using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers.ReflectionEmit;

namespace SimpleContainer.Helpers
{
	internal static class ObjectAccessor
	{
		private static readonly ConcurrentDictionary<Type, TypeAccessor> typeAccessors =
			new ConcurrentDictionary<Type, TypeAccessor>();

		private static readonly Func<Type, TypeAccessor> createTypeAccessor = t =>
		{
			var properties = t.GetProperties().ToDictionary(x => x.Name, MemberAccessorsFactory.GetGetter);
			return new TypeAccessor(properties);
		};

		public static IObjectAccessor Get(object o)
		{
			return o == null ? null : new ObjectAccessorImpl(typeAccessors.GetOrAdd(o.GetType(), createTypeAccessor), o);
		}

		private class ObjectAccessorImpl : IObjectAccessor
		{
			private readonly TypeAccessor typeAccessor;
			private readonly object obj;
			private readonly HashSet<string> used = new HashSet<string>();

			public ObjectAccessorImpl(TypeAccessor typeAccessor, object obj)
			{
				this.typeAccessor = typeAccessor;
				this.obj = obj;
			}

			public bool TryGet(string name, out object value)
			{
				var result = typeAccessor.TryGet(obj, name, out value);
				if (result)
					used.Add(name);
				return result;
			}

			public IEnumerable<string> GetUsed()
			{
				return used;
			}

			public IEnumerable<string> GetUnused()
			{
				return typeAccessor.GetNames().Except(used);
			}
		}

		private class TypeAccessor
		{
			private readonly IDictionary<string, Func<object, object>> properties;

			public TypeAccessor(IDictionary<string, Func<object, object>> properties)
			{
				this.properties = properties;
			}

			public IEnumerable<string> GetNames()
			{
				return properties.Keys;
			}

			public bool TryGet(object o, string name, out object value)
			{
				Func<object, object> accessor;
				if (properties.TryGetValue(name, out accessor))
				{
					value = accessor(o);
					return true;
				}
				value = null;
				return false;
			}
		}
	}
}