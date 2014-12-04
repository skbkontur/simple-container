using NUnit.Framework;
using SimpleContainer.Configuration;

namespace SimpleContainer.Tests
{
	public abstract class ContainerProfilesTest : SimpleContainerTestBase
	{
		public class Simple : ContainerProfilesTest
		{
			public class InMemoryProfile
			{
			}

			public interface IDatabase
			{
			}

			public class InMemoryDatabase : IDatabase
			{
			}

			public class Database : IDatabase
			{
			}

			public class DatabaseConfigurator : IServiceConfigurator<IDatabase>
			{
				public void Configure(ServiceConfigurationBuilder<IDatabase> builder)
				{
					builder.Bind<Database>();
					builder.Profile<InMemoryProfile>().Bind<InMemoryDatabase>();
				}
			}

			[Test]
			public void Test()
			{
				var container = Container();
				Assert.That(container.Get<IDatabase>(), Is.InstanceOf<Database>());

				var inMemoryContainer = Container(null, typeof (InMemoryProfile).Name);
				Assert.That(inMemoryContainer.Get<IDatabase>(), Is.InstanceOf<InMemoryDatabase>());
			}
		}
	}
}