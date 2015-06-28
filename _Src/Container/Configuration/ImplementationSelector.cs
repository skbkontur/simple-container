using System;
using System.Collections.Generic;

namespace SimpleContainer.Configuration
{
	public delegate void ImplementationSelector(Type interfaceType, Type[] implementationTypes,
		List<ImplementationSelectorDecision> decisions);
}