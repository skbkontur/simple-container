using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace SimpleContainer.Tests
{
	public abstract class SimpleContainerConcurrentTest: SimpleContainerTestBase
	{
		public class ConstructorsOfSingletonServicesAreCalledExactlyOnce: SimpleContainerConcurrentTest
		{
			private static int generation;
			private static ConcurrentDictionary<string, bool> createdServices;

			public abstract class ServiceBase
			{
				protected ServiceBase()
				{
					var key = generation + "$$$" + GetType().Name;
					Assert.That(createdServices.ContainsKey(key), Is.False);
					Assert.That(createdServices.TryAdd(key, true));
				}
			}

			public class A: ServiceBase
			{
			}

			public class B: ServiceBase
			{
			}

			public class C: ServiceBase
			{
			}

			protected override void SetUp()
			{
				base.SetUp();
				generation = 0;
				createdServices = new ConcurrentDictionary<string, bool>();
			}

			protected override void TearDown()
			{
				createdServices = null;
				base.TearDown();
			}

			[Test]
			public void Test()
			{
				const int threadCount = 6;
				var testContainer = Container();
				var barrier = new Barrier(threadCount, _ =>
													   {
														   testContainer = Container();
														   generation++;
													   });
				Exception failure = null;
				var threads = Enumerable
					.Range(0, threadCount)
					.Select(_ => new Thread(delegate(object __)
											{
												try
												{
													for (var myGeneration = 0; myGeneration < 1000; myGeneration++)
													{
														try
														{
															for (var j = 0; j < 100; j++)
															{
																testContainer.GetAll<ServiceBase>();
																var implTypes = testContainer.GetImplementationsOf<ServiceBase>();
																foreach (var implType in implTypes)
																	testContainer.Get(implType, null);
															}
														}
														finally
														{
															barrier.SignalAndWait();
														}
														if (failure != null)
															return;
													}
												}
												catch (Exception e)
												{
													failure = e;
												}
											}))
					.ToArray();
				foreach (var thread in threads)
					thread.Start();
				foreach (var thread in threads)
					thread.Join();
				if (failure != null)
					Assert.Fail("source exception:\r\n" + failure);
			}
		}
	}
}