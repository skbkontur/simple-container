using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Configuration
{
	internal static class FileConfigurationParser
	{
		public static Action<ContainerConfigurationBuilder> Parse(Type[] types, string fileName)
		{
			var parseItems = SplitWithTrim(File.ReadAllText(fileName).Replace("\r\n", "\n"), "\n").Select(Parse).ToArray();
			var typesMap = types.ToLookup(x => x.Name);
			return delegate(ContainerConfigurationBuilder builder)
			{
				var context = new ParseContext(builder.RegistryBuilder, typesMap);
				foreach (var item in parseItems)
					item(context);
			};
		}

		private static string[] SplitWithTrim(string s, string by)
		{
			return s.Split(new[] {by}, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
		}

		private static void BindDependency(Type type, string dependencyName, string dependencyText, ParseContext parseContext)
		{
			var serviceConstructor = type.GetConstructor();
			if (!serviceConstructor.isOk)
			{
				var message = string.Format("type [{0}] has ", type.FormatName());
				throw new SimpleContainerException(message + serviceConstructor.errorMessage);
			}
			var formalParameter = serviceConstructor.value.GetParameters().SingleOrDefault(x => x.Name == dependencyName);
			if (formalParameter == null)
			{
				const string message = "type [{0}] has no dependency [{1}]";
				throw new SimpleContainerException(string.Format(message, type.FormatName(), dependencyName));
			}
			var targetType = formalParameter.ParameterType;
			var underlyingType = Nullable.GetUnderlyingType(targetType);
			if (underlyingType != null)
				targetType = underlyingType;
			IConvertible convertible = dependencyText;
			object parsedValue;
			try
			{
				parsedValue = convertible.ToType(targetType, CultureInfo.InvariantCulture);
			}
			catch (Exception e)
			{
				const string message = "can't parse [{0}.{1}] from [{2}] as [{3}]";
				throw new SimpleContainerException(string.Format(message, type.FormatName(), dependencyName,
					dependencyText, formalParameter.ParameterType.FormatName()), e);
			}
			parseContext.GetServiceBuilder(type).BindDependency(dependencyName, parsedValue);
		}

		private static Action<ParseContext> Parse(string line)
		{
			var items = SplitWithTrim(line, "->");
			var fromToken = items[0];
			if (fromToken.StartsWith("["))
			{
				var contractName = fromToken.Substring(1, fromToken.Length - 2);
				return c => c.SetContract(contractName);
			}
			if (items.Length == 1)
				return c => c.GetServiceBuilder(c.ParseType(fromToken)).DontUse();
			var toToken = items[1];
			var fromTokenItems = SplitWithTrim(fromToken, ".");
			if (fromTokenItems.Length > 1)
				return c => BindDependency(c.ParseType(fromTokenItems[0]), fromTokenItems[1], toToken, c);
			return c =>
			{
				var type = c.ParseType(fromToken);
				c.GetServiceBuilder(type).Bind(type, c.ParseType(toToken), true);
			};
		}

		private class ParseContext
		{
			private readonly ConfigurationRegistry.Builder builder;
			private readonly ILookup<string, Type> typesMap;
			private string contractName;

			public ParseContext(ConfigurationRegistry.Builder builder, ILookup<string, Type> typesMap)
			{
				this.builder = builder;
				this.typesMap = typesMap;
			}

			public void SetContract(string name)
			{
				contractName = name;
			}

			public ServiceConfiguration.Builder GetServiceBuilder(Type type)
			{
				var contracts = new List<string>();
				if (!string.IsNullOrEmpty(contractName))
					contracts.Add(contractName);
				return builder.GetConfigurationSet(type).GetBuilder(contracts);
			}

			public Type ParseType(string name)
			{
				var foundTypes = typesMap[name].ToArray();
				if (foundTypes.Length > 1)
				{
					const string messageFormat = "for name [{0}] more than one type found {1}";
					var foundTypesString = foundTypes.Select(x => string.Format("[{0}]", x.FullName)).JoinStrings(", ");
					throw new SimpleContainerException(string.Format(messageFormat, name, foundTypesString));
				}
				if (foundTypes.Length == 0)
				{
					const string messageFormat = "no types found for name [{0}]";
					throw new SimpleContainerException(string.Format(messageFormat, name));
				}
				return foundTypes[0];
			}
		}
	}
}