using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Reflection.ReflectionEmit;

namespace SimpleContainer
{
	public class ObjectAccessors
	{
		private readonly ConcurrentDictionary<Type, IObjectAccessor> accessors = new ConcurrentDictionary<Type, IObjectAccessor>();
		private readonly Func<Type, IObjectAccessor> createParametersAccessor;
		public static ObjectAccessors Instance { get; private set; }

		static ObjectAccessors()
		{
			Instance = new ObjectAccessors();
		}

		public ObjectAccessors()
		{
			createParametersAccessor = CreateParametersAccessor;
		}

		public IObjectAccessor GetAccessor(Type type)
		{
			return accessors.GetOrAdd(type, createParametersAccessor);
		}

		public IEnumerable<KeyValuePair<string, object>> GetValues(object o)
		{
			return o == null
					   ? Enumerable.Empty<KeyValuePair<string, object>>()
					   : GetAccessor(o.GetType()).GetValues(o);
		}

		private class ObjectAccessor: IObjectAccessor
		{
			private readonly IDictionary<string, IMemberAccessor> properties;

			public ObjectAccessor(IDictionary<string, IMemberAccessor> properties)
			{
				this.properties = properties;
			}

			public IEnumerable<KeyValuePair<string, object>> GetValues(object o)
			{
				return properties.Select(x => new KeyValuePair<string, object>(x.Key, x.Value.Get(o)));
			}

			public bool TryGet(object o, string name, out object value)
			{
				IMemberAccessor accessor;
				if (properties.TryGetValue(name, out accessor))
				{
					value = accessor.Get(o);
					return true;
				}
				value = null;
				return false;
			}
		}

		private static IObjectAccessor CreateParametersAccessor(Type type)
		{
			IDictionary<string, IMemberAccessor> properties = type.GetProperties().ToDictionary(x => x.Name, UntypedMemberAccessor.Create);
			return new ObjectAccessor(properties);
		}
	}
}