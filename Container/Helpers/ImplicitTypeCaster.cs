using System;

namespace SimpleContainer.Helpers
{
	internal static class ImplicitTypeCaster
	{
		public static object TryCast(object source, Type destionationType)
		{
			if (destionationType.IsInstanceOfType(source))
				return source;
			var underlyingType = Nullable.GetUnderlyingType(destionationType);
			if (underlyingType != null)
				destionationType = underlyingType;
			if (source is int && destionationType == typeof (long))
				return (long) (int) source;
			return null;
		}
	}
}