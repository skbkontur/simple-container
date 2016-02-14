using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	internal static class ValueFormatter
	{
		public static void WriteValue(ConstructionLogContext context, object value, bool isTop)
		{
			var formattedValue = FormatAsSimpleType(value, context);
			if (formattedValue != null)
			{
				if (isTop && value != null && context.ValueFormatters.ContainsKey(value.GetType()))
					context.Writer.WriteMeta(" const");
				context.Writer.WriteMeta(" -> " + formattedValue);
				return;
			}
			if (isTop)
				context.Writer.WriteMeta(" const");
			context.Indent++;
			var enumerable = value as IEnumerable;
			if (enumerable == null)
			{
				var properties = value.GetType()
					.GetProperties(BindingFlags.Public | BindingFlags.Instance)
					.Where(x => x.CanRead && (x.CanWrite || IsAutoProperty(x)))
					.ToArray();
				if (properties.Length > 0)
					WriteMembers(context, properties, value);
				else
					WriteMembers(context, value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance), value);
			}
			else
				foreach (var item in enumerable)
				{
					context.Writer.WriteNewLine();
					context.WriteIndent();
					formattedValue = FormatAsSimpleType(item, context);
					if (formattedValue == null)
					{
						context.Writer.WriteName("item");
						WriteValue(context, item, false);
					}
					else
						context.Writer.WriteMeta(formattedValue);
				}
			context.Indent--;
		}

		private static bool IsAutoProperty(PropertyInfo propertyInfo)
		{
			var getMethod = propertyInfo.GetGetMethod(true);
			var setMethod = propertyInfo.GetSetMethod(true);
			return getMethod != null &&
			       setMethod != null &&
			       getMethod.IsDefined<CompilerGeneratedAttribute>() &&
			       setMethod.IsDefined<CompilerGeneratedAttribute>();
		}

		private static void WriteMembers(ConstructionLogContext context, IEnumerable<MemberInfo> members, object value)
		{
			foreach (var m in members)
			{
				var propVal = m is FieldInfo ? ((FieldInfo) m).GetValue(value) : ((PropertyInfo) m).GetValue(value);
				context.Writer.WriteNewLine();
				context.WriteIndent();
				context.Writer.WriteName(m.Name);
				WriteValue(context, propVal, false);
			}
		}

		private static string FormatAsSimpleType(object value, ConstructionLogContext context)
		{
			Func<object, string> formatter;
			return value == null || value.GetType().IsSimpleType()
				? InternalHelpers.DumpValue(value)
				: context.ValueFormatters.TryGetValue(value.GetType(), out formatter)
					? formatter(value)
					: null;
		}
	}
}