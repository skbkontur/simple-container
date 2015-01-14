using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public class InstanceFilterTest : ContractsTest
	{
		public class FileAccessor
		{
			public readonly string fileName;

			public FileAccessor(string fileName)
			{
				this.fileName = fileName;
			}
		}

		public class Wrap
		{
			public readonly IEnumerable<FileAccessorWrap> fileAccessors;

			public Wrap([TestContract("all")] IEnumerable<FileAccessorWrap> fileAccessors)
			{
				this.fileAccessors = fileAccessors;
			}
		}

		public class FileAccessorWrap
		{
			public readonly FileAccessor fileAccessor;

			public FileAccessorWrap(FileAccessor fileAccessor)
			{
				this.fileAccessor = fileAccessor;
			}

			public bool IsValid()
			{
				return fileAccessor.fileName.StartsWith("ww");
			}
		}

		public class Configurator : IContainerConfigurator
		{
			public void Configure(ConfigurationContext context, ContainerConfigurationBuilder builder)
			{
				builder.Contract("all").UnionOf("c1", "c2", "c3");
				builder.Contract("c1").BindDependency<FileAccessor>("fileName", "qq");
				builder.Contract("c2").BindDependency<FileAccessor>("fileName", "ww1");
				builder.Contract("c3").BindDependency<FileAccessor>("fileName", "ww2");
				builder.WithInstanceFilter<FileAccessorWrap>(a => a.IsValid());
			}
		}

		[Test]
		public void Test()
		{
			var container = Container();
			var instance = container.Get<Wrap>();
			Assert.That(instance.fileAccessors.Select(x => x.fileAccessor.fileName).ToArray(), Is.EqualTo(new[] {"ww1", "ww2"}));
			Assert.That(container.Resolve<FileAccessorWrap>("c1").GetConstructionLog(),
				Is.EqualTo("FileAccessorWrap[c1]->[c1]! - instance filter\r\n\tFileAccessor[c1]\r\n\t\tfileName -> qq"));
			Assert.That(container.Resolve<FileAccessorWrap>("c2").GetConstructionLog(),
				Is.EqualTo("FileAccessorWrap[c2]->[c2] - instance filter\r\n\tFileAccessor[c2]\r\n\t\tfileName -> ww1"));
		}
	}
}