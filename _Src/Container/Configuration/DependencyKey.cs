using System;
using SimpleContainer.Helpers;

namespace SimpleContainer.Configuration
{
	internal struct DependencyKey : IEquatable<DependencyKey>
	{
		public readonly string name;
		public readonly Type type;

		public DependencyKey(string name)
			: this(name, null)
		{
		}

		public DependencyKey(Type type)
			: this(null, type)
		{
		}

		private DependencyKey(string name, Type type)
		{
			this.name = name;
			this.type = type;
		}

		public bool Equals(DependencyKey other)
		{
			return string.Equals(name, other.name) && type == other.type;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			return obj is DependencyKey && Equals((DependencyKey) obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((name != null ? name.GetHashCode() : 0)*397) ^ (type != null ? type.GetHashCode() : 0);
			}
		}

		public static bool operator ==(DependencyKey left, DependencyKey right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(DependencyKey left, DependencyKey right)
		{
			return !left.Equals(right);
		}

		public override string ToString()
		{
			return name == null ? "type=" + type.FormatName() : "name=" + name;
		}
	}
}