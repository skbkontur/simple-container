using System;

namespace SimpleContainer.Implementation
{
	internal struct ImplementationType : IEquatable<ImplementationType>
	{
		public Type type;
		public string comment;
		public bool accepted;

		public static ImplementationType Accepted(Type type, string comment = null)
		{
			return new ImplementationType
			{
				type = type,
				comment = comment,
				accepted = true
			};
		}

		public static ImplementationType Rejected(Type type, string comment)
		{
			return new ImplementationType
			{
				type = type,
				comment = comment,
				accepted = false
			};
		}

		public bool Equals(ImplementationType other)
		{
			return type == other.type;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			return obj is ImplementationType && Equals((ImplementationType) obj);
		}

		public override int GetHashCode()
		{
			return (type != null ? type.GetHashCode() : 0);
		}

		public static bool operator ==(ImplementationType left, ImplementationType right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(ImplementationType left, ImplementationType right)
		{
			return !left.Equals(right);
		}
	}
}