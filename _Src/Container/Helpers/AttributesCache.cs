using System;
using System.Linq;
using System.Reflection;
using SimpleContainer.Implementation.Hacks;

namespace SimpleContainer.Helpers
{
	internal class AttributesCache
	{
		public static readonly AttributesCache instance = new AttributesCache();
		private readonly NonConcurrentDictionary<Key, Attribute[]> cache = new NonConcurrentDictionary<Key, Attribute[]>();

		public AttributesCache()
		{
			createDelegate = CreateCustomAttributes;
		}

		private readonly Func<Key, Attribute[]> createDelegate;

		private static Attribute[] CreateCustomAttributes(Key key)
		{
			var attributeProvider = key.attributeProvider;
			var type = attributeProvider as Type;
			if (type != null)
				return type.GetCustomAttributes(key.attributeType, key.inherit);
			var param = attributeProvider as ParameterInfo;
			if (param != null)
				return param.GetCustomAttributes(key.attributeType, key.inherit).ToArray();
			var member = attributeProvider as MemberInfo;
			if (member != null)
				return member.GetCustomAttributes(key.attributeType, key.inherit).ToArray();
			throw new NotSupportedException(string.Format("invalid type [{0}]", attributeProvider.GetType().FormatName()));
		}

		public Attribute[] GetCustomAttributes(object attributeProvider, Type attributeType, bool inherit)
		{
			return cache.GetOrAdd(new Key(attributeProvider, attributeType, inherit), createDelegate);
		}

		private struct Key
		{
			public readonly object attributeProvider;
			public readonly Type attributeType;
			public readonly bool inherit;

			public Key(object attributeProvider, Type attributeType, bool inherit)
			{
				this.attributeProvider = attributeProvider;
				this.attributeType = attributeType;
				this.inherit = inherit;
			}

			private bool Equals(Key other)
			{
				var localInherit = inherit;
				return attributeProvider.Equals(other.attributeProvider) && attributeType == other.attributeType &&
				       localInherit.Equals(other.inherit);
			}

			public override bool Equals(object obj)
			{
				if (ReferenceEquals(null, obj)) return false;
				return obj is Key && Equals((Key) obj);
			}

			public override int GetHashCode()
			{
				unchecked
				{
					var hashCode = (attributeProvider != null ? attributeProvider.GetHashCode() : 0);
					hashCode = (hashCode*397) ^ (attributeType != null ? attributeType.GetHashCode() : 0);
					var localInherit = inherit;
					hashCode = (hashCode*397) ^ localInherit.GetHashCode();
					return hashCode;
				}
			}
		}
	}
}