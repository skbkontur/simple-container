using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SimpleContainer.Configuration;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class InstanceFilterTest : SimpleContainerTestBase
	{
		public class Basic : InstanceFilterTest
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
				Assert.That(instance.fileAccessors.Select(x => x.fileAccessor.fileName).ToArray(), Is.EqualTo(new[] { "ww1", "ww2" }));
				Assert.That(container.Resolve<Wrap>().GetConstructionLog(), Is.EqualTo(TestHelpers.FormatMessage(@"
Wrap
	FileAccessorWrap[all]++
		!FileAccessorWrap[c1] - instance filter
			FileAccessor[c1]
				fileName -> qq
		FileAccessorWrap[c2]
			FileAccessor[c2]
				fileName -> ww1
		FileAccessorWrap[c3]
			FileAccessor[c3]
				fileName -> ww2")));

				Assert.That(container.Resolve<FileAccessorWrap>("c1").GetConstructionLog(), Is.EqualTo(TestHelpers.FormatMessage(@"
!FileAccessorWrap[c1] - instance filter
	FileAccessor[c1]
		fileName -> qq")));

				Assert.That(container.Resolve<FileAccessorWrap>("c2").GetConstructionLog(), Is.EqualTo(TestHelpers.FormatMessage(@"
FileAccessorWrap[c2]
	FileAccessor[c2]
		fileName -> ww1")));
			}
		}

		public class Contracts : InstanceFilterTest
		{
			[TestContract("c1")]
			public class A
			{
				public readonly B b;

				public A([TestContract("c2")] B b)
				{
					this.b = b;
				}
			}

			public class B
			{
				public readonly int value;

				public B(int value)
				{
					this.value = value;
				}
			}

			[Test]
			public void Test()
			{
				var container = Container(b =>
				{
					b.Contract("c1").BindDependency<B>("value", 42);
					b.Contract("c1").WithInstanceFilter<B>(v => v.value != 42);
				});
				var instances = container.GetAll<A>();
				Assert.That(instances.ToArray(), Is.Empty);
			}
		}
	}
}