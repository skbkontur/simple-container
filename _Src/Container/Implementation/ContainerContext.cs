using System;
using System.Collections.Generic;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class ContainerContext
	{
		public Dictionary<Type, Func<object, string>> valueFormatters;
		public TypesList typesList;
		public LogInfo infoLogger;
	}
}