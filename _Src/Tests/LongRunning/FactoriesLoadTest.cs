using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests.LongRunning
{
	public class FactoriesLoadTest : SimpleContainerTestBase
	{
		public interface IA
		{
		}

		public class A : IA
		{
		}

		public class B1
		{
		}

		public class B2
		{
		}

		public class B3
		{
			public readonly B4 b4;

			public B3(B4 b4)
			{
				this.b4 = b4;
			}
		}

		public class B4
		{
		}

		public class B5
		{
			public readonly B2 b2;

			public B5(B2 b2)
			{
				this.b2 = b2;
			}
		}

		public class Invoker
		{
			private readonly Func<Type, IA> factory;

			public Invoker(Func<Type, IA> factory)
			{
				this.factory = factory;
			}

			public IA Create(Type t)
			{
				return factory(t);
			}
		}

		[Test]
		public void TestCreateClass()
		{
			var container = Container();
			var invoker = container.Get<Invoker>();
			var stopwatch = Stopwatch.StartNew();
			Parallel.For(0, 1000000, new ParallelOptions {MaxDegreeOfParallelism = Environment.ProcessorCount},
				_ => invoker.Create(typeof (A)));
			stopwatch.Stop();
			Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(400)));
		}

		[Test]
		public void TestCreateInterface()
		{
			var container = Container();
			var factory = container.Get<Func<IA>>();
			var stopwatch = Stopwatch.StartNew();
			Parallel.For(0, 1000000, new ParallelOptions {MaxDegreeOfParallelism = Environment.ProcessorCount},
				_ => factory());
			stopwatch.Stop();
			Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(400)));
		}
	}
}