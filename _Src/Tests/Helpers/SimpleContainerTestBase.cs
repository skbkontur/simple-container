using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using SimpleContainer.Configuration;
using SimpleContainer.Interface;

namespace SimpleContainer.Tests.Helpers
{
	public abstract class SimpleContainerTestBase : UnitTestBase
	{
		protected List<IDisposable> disposables;

		protected override void SetUp()
		{
			base.SetUp();
			disposables = new List<IDisposable>();
			LogBuilder = new StringBuilder();
		}

		public static StringBuilder LogBuilder { get; private set; }

		protected override void TearDown()
		{
			if (disposables != null)
				foreach (var disposable in disposables)
					disposable.Dispose();
			base.TearDown();
		}

		protected IStaticContainer CreateStaticContainer(Action<ContainerFactory> configureContainerFactory = null,
			Type profile = null)
		{
			var targetTypes = GetType().GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public);
			var factory = new ContainerFactory()
				.WithAssembliesFilter(x => x.Name.StartsWith("SimpleContainer"))
				.WithProfile(profile);
			if (configureContainerFactory != null)
				configureContainerFactory(factory);
			return factory.FromTypes(targetTypes);
		}

		protected IContainer Container(Action<ContainerConfigurationBuilder> configure = null, Type profile = null, IParametersSource parameters = null)
		{
			var staticContainer = CreateStaticContainer(null, profile);
			disposables.Add(staticContainer);
			var result = LocalContainer(staticContainer, parameters, configure);
			disposables.Add(result);
			return result;
		}

		protected static IContainer LocalContainer(IStaticContainer staticContainer,
			IParametersSource parameters = null, Action<ContainerConfigurationBuilder> configure = null)
		{
			return staticContainer.CreateLocalContainer(null, Assembly.GetExecutingAssembly(), parameters, configure);
		}
	}
}