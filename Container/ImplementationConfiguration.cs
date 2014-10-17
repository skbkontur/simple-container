using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Reflection;

namespace SimpleContainer
{
	public class ImplementationConfiguration
	{
		private readonly List<ImplentationDependencyConfiguration> dependencies = new List<ImplentationDependencyConfiguration>();
		public bool DontUseIt { get; set; }

		public ImplentationDependencyConfiguration GetByKeyOrNull(string key)
		{
			return dependencies.SingleOrDefault(x => x.Key == key);
		}

		public ImplentationDependencyConfiguration GetOrNull(ParameterInfo parameter)
		{
			return GetByKeyOrNull(parameter.Name + " name") ?? GetByKeyOrNull(parameter.ParameterType.FormatName() + " type");
		}

		public ImplentationDependencyConfiguration GetOrCreateByKey(string key)
		{
			var result = GetByKeyOrNull(key);
			if (result == null)
				dependencies.Add(result = new ImplentationDependencyConfiguration { Key = key });
			return result;
		}
	}
}