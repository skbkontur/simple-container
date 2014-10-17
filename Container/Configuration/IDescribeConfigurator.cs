using System;

namespace SimpleContainer.Configuration
{
	public interface IDescribeConfigurator
	{
		bool IsPrimary(Type configuratorType);
	}
}