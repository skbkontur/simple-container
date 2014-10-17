using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace SimpleContainer.Reflection
{
	public class AttributesCache
	{
		public static readonly AttributesCache instance = new AttributesCache();
		private readonly ConcurrentDictionary<Key, object> cache = new ConcurrentDictionary<Key, object>();

		public AttributesCache()
		{
			createDelegate = CreateCustomAttributes;
		}

		private readonly Func<Key, object> createDelegate;

		private static object CreateCustomAttributes(Key key)
		{
			return key.attributeProvider.GetCustomAttributes(key.attributeType, key.inherit);
		}

		public object GetCustomAttributes(ICustomAttributeProvider attributeProvider, Type attributeType, bool inherit)
		{
			return cache.GetOrAdd(new Key(attributeProvider, attributeType, inherit), createDelegate);
		}

		private struct Key
		{
			public readonly ICustomAttributeProvider attributeProvider;
			public readonly Type attributeType;
			public readonly bool inherit;

			public Key(ICustomAttributeProvider attributeProvider, Type attributeType, bool inherit)
			{
				this.attributeProvider = attributeProvider;
				this.attributeType = attributeType;
				this.inherit = inherit;
			}

			private bool Equals(Key other)
			{
				var localInherit = inherit;
				return attributeProvider.Equals(other.attributeProvider) && attributeType == other.attributeType && localInherit.Equals(other.inherit);
			}

			public override bool Equals(object obj)
			{
				if (ReferenceEquals(null, obj)) return false;
				return obj is Key && Equals((Key)obj);
			}

			public override int GetHashCode()
			{
				unchecked
				{
					var hashCode = (attributeProvider != null ? attributeProvider.GetHashCode() : 0);
					hashCode = (hashCode * 397) ^ (attributeType != null ? attributeType.GetHashCode() : 0);
					var localInherit = inherit;
					hashCode = (hashCode * 397) ^ localInherit.GetHashCode();
					return hashCode;
				}
			}
		}
	}
}