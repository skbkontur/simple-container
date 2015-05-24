using System;
using System.Collections.Generic;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Configuration
{
	public class StaticServicesConfigurator
	{
		private readonly bool isStatic;
		private readonly ISet<Type> staticServices = new HashSet<Type>();

		public StaticServicesConfigurator(bool isStatic)
		{
			this.isStatic = isStatic;
		}

		public ISet<Type> GetStaticServices()
		{
			return staticServices;
		}

		public void MakeStatic(Type type)
		{
			if (!isStatic)
			{
				const string messageFormat = "can't make type [{0}] static using non static configurator";
				throw new SimpleContainerException(string.Format(messageFormat, type.FormatName()));
			}
			staticServices.Add(type);
		}
	}
}