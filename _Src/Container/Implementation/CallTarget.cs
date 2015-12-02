using System;
using System.Reflection;

namespace SimpleContainer.Implementation
{
	internal struct CallTarget
	{
		public MethodBase method;
		public object self;
		public object[] actualArguments;
		public Func<ContainerService.Builder, object> factory;

		public static CallTarget M(MethodBase method, object self, object[] actualArguments)
		{
			return new CallTarget
			{
				method = method,
				self = self,
				actualArguments = actualArguments
			};
		}

		public static CallTarget F(Func<ContainerService.Builder, object> f)
		{
			return new CallTarget
			{
				factory = f
			};
		}
	}
}