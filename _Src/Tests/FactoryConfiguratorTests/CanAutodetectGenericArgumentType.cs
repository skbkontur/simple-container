using System;
using NUnit.Framework;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.FactoryConfiguratorTests
{
	public class CanAutodetectGenericArgumentType : SimpleContainerTestBase
	{
		public class SomeService
		{
			private readonly Func<object, IGenericServiceDecorator> getDecorator;

			public SomeService(Func<object, IGenericServiceDecorator> getDecorator)
			{
				this.getDecorator = getDecorator;
			}

			public string Run(object genericService, string parameter)
			{
				return getDecorator(new {genericService, parameter}).DoSomething();
			}

			public class GenericServiceDecorator<T> : IGenericServiceDecorator
			{
				private readonly IGenericService<T> genericService;
				private readonly string parameter;

				public GenericServiceDecorator(IGenericService<T> genericService, string parameter)
				{
					this.genericService = genericService;
					this.parameter = parameter;
				}

				public string DoSomething()
				{
					return genericService.Describe() + " " + parameter;
				}
			}
		}

		public interface IGenericServiceDecorator
		{
			string DoSomething();
		}

		public interface IGenericService<T>
		{
			string Describe();
		}

		public class IntGenericService : IGenericService<int>
		{
			public string Describe()
			{
				return "i'm int";
			}
		}

		[Test]
		public void Test()
		{
			var container = Container();
			var someService = container.Get<SomeService>();
			var intGenericService = container.Get<IntGenericService>();
			Assert.That(someService.Run(intGenericService, "testParameter"), Is.EqualTo("i'm int testParameter"));
		}
	}
}