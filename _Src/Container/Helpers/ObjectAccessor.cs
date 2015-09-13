using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Helpers.ReflectionEmit;
using SimpleContainer.Implementation.Hacks;

namespace SimpleContainer.Helpers
{
	internal static class ObjectAccessor
	{
		private static readonly NonConcurrentDictionary<Type, Dictionary<string, Property>> typeAccessors =
			new NonConcurrentDictionary<Type, Dictionary<string, Property>>();

		private static readonly Func<Type, Dictionary<string, Property>> createTypeAccessor = t => t.GetProperties()
			.ToDictionary(x => x.Name, x => new Property
			{
				getter = MemberAccessorsFactory.GetGetter(x),
				type = x.PropertyType
			});

		public static IObjectAccessor Get(object o)
		{
			return o == null ? null : new ObjectAccessorImpl(typeAccessors.GetOrAdd(o.GetType(), createTypeAccessor), o);
		}

		private class ObjectAccessorImpl : IObjectAccessor
		{
			private readonly Dictionary<string, Property> properties;
			private readonly object obj;
			private readonly HashSet<string> used = new HashSet<string>();

			public ObjectAccessorImpl(Dictionary<string, Property> properties, object obj)
			{
				this.properties = properties;
				this.obj = obj;
			}

			public bool TryGet(string name, out ValueWithType value)
			{
				Property property;
				if (!properties.TryGetValue(name, out property))
				{
					value = default (ValueWithType);
					return false;
				}
				value = new ValueWithType
				{
					type = property.type,
					value = property.getter(obj)
				};
				used.Add(name);
				return true;
			}

			public IEnumerable<string> GetUsed()
			{
				return used;
			}

			public IEnumerable<string> GetUnused()
			{
				return properties.Keys.Except(used);
			}
		}

		private class Property
		{
			public Func<object, object> getter;
			public Type type;
		}
	}
}