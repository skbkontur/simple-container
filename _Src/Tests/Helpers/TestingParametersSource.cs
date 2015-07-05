using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimpleContainer.Interface;

namespace SimpleContainer.Tests.Helpers
{
	public class TestingParametersSource : IParametersSource
	{
		private readonly IDictionary<string, object> values;

		public TestingParametersSource(IDictionary<string, object> values)
		{
			this.values = values;
		}

		public bool TryGet(string name, Type type, out object value)
		{
			if (!values.TryGetValue(name, out value))
				return false;
			Assert.That(value.GetType(), Is.SameAs(type));
			return true;
		}
	}
}