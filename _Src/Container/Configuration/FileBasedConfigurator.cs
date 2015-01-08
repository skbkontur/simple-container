using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using SimpleContainer.Helpers;
using SimpleContainer.Implementation;

namespace SimpleContainer.Configuration
{
	internal static class FileConfigurationParser
	{
		public static Action<Func<Type, bool>, ContainerConfigurationBuilder> Parse(Type[] types, string fileName)
		{
			var parseItems = SplitWithTrim(File.ReadAllText(fileName), Environment.NewLine).Select(Parse).ToArray();
			var typesMap = types.ToLookup(x => x.Name);
			return delegate(Func<Type, bool> f, ContainerConfigurationBuilder builder)
			{
				var context = new ParseContext(builder, typesMap, f);
				foreach (var item in parseItems)
					item(context);
			};
		}

		private static string[] SplitWithTrim(string s, string by)
		{
			return s.Split(new[] {by}, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
		}

		private static void BindDependency(Type type, string dependencyName, string dependencyText,
			IInternalConfigurationBuilder builder)
		{
			var implementation = new Implementation.Implementation(type);
			ConstructorInfo constructor;
			if (!implementation.TryGetConstructor(out constructor))
			{
				var messageFormat = implementation.publicConstructors.Length == 0
					? "type [{0}] has no public ctors, maybe ctor is private?"
					: "type [{0}] has many public ctors, maybe some of them should be made private?";
				throw new SimpleContainerException(string.Format(messageFormat, type.FormatName()));
			}
			var formalParameter = constructor.GetParameters().SingleOrDefault(x => x.Name == dependencyName);
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
			builder.BindDependency(type, dependencyName, parsedValue);
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
				return c => c.Builder.DontUse(c.ParseType(fromToken));
			var toToken = items[1];
			var fromTokenItems = SplitWithTrim(fromToken, ".");
			if (fromTokenItems.Length > 1)
				return c => BindDependency(c.ParseType(fromTokenItems[0]), fromTokenItems[1], toToken, c.Builder);
			return c => c.Builder.Bind(c.ParseType(fromToken), c.ParseType(toToken));
		}

		private class ParseContext
		{
			private readonly ContainerConfigurationBuilder builder;
			private readonly ILookup<string, Type> typesMap;
			private readonly Func<Type, bool> filter;
			private string contractName;

			public ParseContext(ContainerConfigurationBuilder builder, ILookup<string, Type> typesMap, Func<Type, bool> filter)
			{
				this.builder = builder;
				this.typesMap = typesMap;
				this.filter = filter;
			}

			public void SetContract(string name)
			{
				contractName = name;
			}

			public IInternalConfigurationBuilder Builder
			{
				get
				{
					return string.IsNullOrEmpty(contractName)
						? (IInternalConfigurationBuilder) builder
						: builder.Contract(contractName);
				}
			}

			public Type ParseType(string name)
			{
				var foundTypes = typesMap[name].Where(filter).ToArray();
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