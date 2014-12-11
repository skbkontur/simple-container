using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Helpers;

namespace SimpleContainer.Configuration
{
	internal class ImplementationConfiguration
	{
		private readonly List<ImplentationDependencyConfiguration> dependencies =
			new List<ImplentationDependencyConfiguration>();

		public bool DontUseIt { get; set; }

		public ImplentationDependencyConfiguration GetByKeyOrNull(string key)
		{
			return dependencies.SingleOrDefault(x => x.Key == key);
		}

		public ImplentationDependencyConfiguration GetOrNull(ParameterInfo parameter)
		{
			var result = GetByKeyOrNull(InternalHelpers.ByNameDependencyKey(parameter.Name)) ??
			             GetByKeyOrNull(InternalHelpers.ByTypeDependencyKey(parameter.ParameterType));
			if (result != null)
				result.Used = true;
			return result;
		}

		public ImplentationDependencyConfiguration GetOrCreateByKey(string key)
		{
			var result = GetByKeyOrNull(key);
			if (result == null)
				dependencies.Add(result = new ImplentationDependencyConfiguration {Key = key});
			return result;
		}

		public IEnumerable<string> GetUnusedDependencyConfigurationKeys()
		{
			return dependencies.Where(x => !x.Used).Select(x => x.Key);
		}

		public Func<object, bool> InstanceFilter { get; set; }
	}
}