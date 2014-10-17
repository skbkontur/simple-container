using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using SimpleContainer.Helpers;

namespace SimpleContainer.Reflection.ReflectionEmit
{
	public static class RuntimeTypeBuilder
	{
		private static readonly AssemblyName assemblyName;
		private static readonly ModuleBuilder moduleBuilder;
		private static readonly Dictionary<string, Type> builtTypes;

		static RuntimeTypeBuilder()
		{
			builtTypes = new Dictionary<string, Type>();
			assemblyName = new AssemblyName {Name = "DynamicTypes"};
			moduleBuilder = Thread.GetDomain().DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run).DefineDynamicModule(assemblyName.Name);
		}

		public static Type GetRuntimeType(IDictionary<string, Type> fields, string typeName = null)
		{
			if (fields == null)
				throw new ArgumentNullException("fields");
			if (fields.Count == 0)
				throw new ArgumentOutOfRangeException("fields", "fields must have at least 1 field definition");

			lock (builtTypes)
			{
				string className = typeName ?? GetTypeKey(fields);
				if (builtTypes.ContainsKey(className))
					return builtTypes[className];

				TypeBuilder typeBuilder = moduleBuilder.DefineType(className,
				                                                   TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Serializable);

				fields.ForEach(pair => typeBuilder.DefineField(pair.Key, pair.Value, FieldAttributes.Public));
				return builtTypes[className] = typeBuilder.CreateType();
			}
		}

		private static string GetTypeKey(IEnumerable<KeyValuePair<string, Type>> fields)
		{
			return fields
				.OrderBy(x => x.Key)
				.Select(x => x.Key + ":" + x.Value.Name)
				.JoinStrings(";");
		}
	}
}