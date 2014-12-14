using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using SimpleContainer.Configuration;
using SimpleContainer.Factories;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;

namespace SimpleContainer.Implementation
{
	//todo отпилить этап анализа зависимостей => meta, contractUsage + factories
	//todo перевести на явную стек машину
	//todo заинлайнить GenericConfigurator
	//todo обработка запросов на явные generic-и
	//todo логировать значения simple-типов, заюзанные через конфигурирование
	//todo избавиться от идиотского EndResolveDependencies
	internal class SimpleContainer : IContainer
	{
		private static readonly ConcurrentDictionary<MethodBase, Func<object, object[], object>> compiledMethods =
			new ConcurrentDictionary<MethodBase, Func<object, object[], object>>();

		private static readonly Func<MethodBase, Func<object, object[], object>> compileMethodDelegate =
			ReflectionHelpers.EmitCallOf;

		private static readonly Func<CacheKey, ContainerService> createContainerServiceDelegate =
			k => new ContainerService(k.type);

		private static readonly IFactoryPlugin[] factoryPlugins =
		{
			new SimpleFactoryPlugin(),
			new FactoryWithArgumentsPlugin()
		};

		protected readonly IContainerConfiguration configuration;
		protected readonly IInheritanceHierarchy inheritors;
		private readonly StaticContainer staticContainer;
		internal readonly CacheLevel cacheLevel;

		private readonly ConcurrentDictionary<CacheKey, ContainerService> instanceCache =
			new ConcurrentDictionary<CacheKey, ContainerService>();

		private readonly Func<CacheKey, ContainerService> createInstanceDelegate;
		private readonly DependenciesInjector dependenciesInjector;
		private int topSortIndex;
		private bool disposed;

		public SimpleContainer(IContainerConfiguration configuration, IInheritanceHierarchy inheritors,
			StaticContainer staticContainer, CacheLevel cacheLevel)
		{
			this.configuration = configuration;
			this.inheritors = inheritors;
			this.staticContainer = staticContainer;
			this.cacheLevel = cacheLevel;
			dependenciesInjector = new DependenciesInjector(this);
			createInstanceDelegate = delegate(CacheKey key)
			{
				var context = new ResolutionContext(configuration, key.contracts);
				return ResolveSingleton(key.type, null, context);
			};
		}

		public object Get(Type serviceType, IEnumerable<string> contracts)
		{
			EnsureNotDisposed();
			Type enumerableItem;
			return TryUnwrapEnumerable(serviceType, out enumerableItem)
				? GetAll(enumerableItem)
				: GetInternal(new CacheKey(serviceType, InternalHelpers.ToInternalContracts(contracts, serviceType)))
					.SingleInstance(false);
		}

		private ContainerService GetInternal(CacheKey cacheKey)
		{
			var result = instanceCache.GetOrAdd(cacheKey, createInstanceDelegate);
			return result.WaitForSuccessfullResolve() ? result : createInstanceDelegate(cacheKey);
		}

		public IEnumerable<object> GetAll(Type serviceType)
		{
			EnsureNotDisposed();
			return GetInternal(new CacheKey(serviceType, null)).AsEnumerable();
		}

		internal ContainerService Create(Type type, IEnumerable<string> contracts, object arguments, ResolutionContext context)
		{
			EnsureNotDisposed();
			context = context ?? new ResolutionContext(configuration, InternalHelpers.ToInternalContracts(contracts, type));
			var result = ContainerService.ForFactory(type, arguments);
			context.Instantiate(null, result, this);
			if (result.Arguments != null)
			{
				var unused = result.Arguments.GetUnused().ToArray();
				if (unused.Any())
					context.Throw("arguments [{0}] are not used", unused.JoinStrings(","));
			}
			return result;
		}

		public object Create(Type type, IEnumerable<string> contracts, object arguments)
		{
			return Create(type, contracts, arguments, null).SingleInstance(false);
		}

		public IEnumerable<Type> GetImplementationsOf(Type interfaceType)
		{
			EnsureNotDisposed();
			var interfaceConfiguration = configuration.GetOrNull<InterfaceConfiguration>(interfaceType);
			if (interfaceConfiguration != null && interfaceConfiguration.ImplementationTypes != null)
				return interfaceConfiguration.ImplementationTypes;
			var result = inheritors.GetOrNull(interfaceType);
			return result != null
				? result.Where(delegate(Type type)
				{
					var implementationConfiguration = configuration.GetOrNull<ImplementationConfiguration>(type);
					return implementationConfiguration == null || !implementationConfiguration.DontUseIt;
				}).ToArray()
				: Type.EmptyTypes;
		}

		public void BuildUp(object target)
		{
			EnsureNotDisposed();
			dependenciesInjector.BuildUp(target);
		}

		public void DumpConstructionLog(Type type, IEnumerable<string> contracts, bool entireResolutionContext,
			ISimpleLogWriter writer)
		{
			EnsureNotDisposed();
			ContainerService containerService;
			var cacheKey = new CacheKey(type, InternalHelpers.ToInternalContracts(contracts, type));
			if (!instanceCache.TryGetValue(cacheKey, out containerService)) return;
			if (entireResolutionContext)
				containerService.Context.Format(null, null, writer);
			else
				containerService.Context.Format(type, cacheKey.contractsKey, writer);
		}

		public IEnumerable<ServiceInstance<object>> GetClosure(Type type, IEnumerable<string> contracts)
		{
			EnsureNotDisposed();

			var cacheKey = new CacheKey(type, InternalHelpers.ToInternalContracts(contracts, type));
			ContainerService containerService;
			return instanceCache.TryGetValue(cacheKey, out containerService) && containerService.WaitForSuccessfullResolve()
				? Utils.Closure(containerService, s => s.Dependencies ?? Enumerable.Empty<ContainerService>())
					.Where(x => x.FinalUsedContracts != null)
					.OrderBy(x => x.TopSortIndex)
					.SelectMany(x => x.GetInstances())
					.Distinct(new ServiceInstanceEqualityComparer())
					.ToArray()
				: Enumerable.Empty<ServiceInstance<object>>();
		}

		private IEnumerable<ServiceInstance<object>> GetInstanceCache(Type interfaceType)
		{
			var result = instanceCache.Values
				.Where(x => x.WaitForSuccessfullResolve() && !x.Type.IsAbstract && interfaceType.IsAssignableFrom(x.Type))
				.ToList();
			result.Sort((a, b) => a.TopSortIndex.CompareTo(b.TopSortIndex));
			return result.SelectMany(x => x.GetInstances()).Distinct(new ServiceInstanceEqualityComparer()).ToArray();
		}

		private class ServiceInstanceEqualityComparer : IEqualityComparer<ServiceInstance<object>>
		{
			public bool Equals(ServiceInstance<object> x, ServiceInstance<object> y)
			{
				return ReferenceEquals(x.Instance, y.Instance);
			}

			public int GetHashCode(ServiceInstance<object> obj)
			{
				return obj.Instance.GetHashCode();
			}
		}

		public IContainer Clone()
		{
			EnsureNotDisposed();
			return new SimpleContainer(configuration, inheritors, staticContainer, cacheLevel);
		}

		internal virtual CacheLevel GetCacheLevel(Type type)
		{
			return staticContainer.GetCacheLevel(type);
		}

		internal ContainerService ResolveSingleton(Type type, string name, ResolutionContext context)
		{
			if (cacheLevel == CacheLevel.Local && GetCacheLevel(type) == CacheLevel.Static)
				return staticContainer.ResolveSingleton(type, name, context);
			var cacheKey = new CacheKey(type, context.RequiredContractNames());
			var result = instanceCache.GetOrAdd(cacheKey, createContainerServiceDelegate);
			if (result.AcquireInstantiateLock())
				try
				{
					context.Instantiate(name, result, this);
					result.InstantiatedSuccessfully(Interlocked.Increment(ref topSortIndex));
				}
				catch
				{
					result.InstantiatedUnsuccessfully();
					throw;
				}
				finally
				{
					result.ReleaseInstantiateLock();
				}
			return result;
		}

		internal void Instantiate(ContainerService service)
		{
			if (ReflectionHelpers.simpleTypes.Contains(service.Type))
				service.Throw("can't create simple type");
			if (service.Type == typeof (IContainer))
			{
				service.AddInstance(this);
				service.EndResolveDependencies();
				return;
			}
			var interfaceConfiguration = service.Context.GetConfiguration<InterfaceConfiguration>(service.Type);
			IEnumerable<Type> implementationTypes = null;
			var useAutosearch = false;
			if (interfaceConfiguration != null)
			{
				if (interfaceConfiguration.ImplementationAssigned)
				{
					service.AddInstance(interfaceConfiguration.Implementation);
					service.EndResolveDependencies();
					return;
				}
				if (interfaceConfiguration.Factory != null)
				{
					service.AddInstance(interfaceConfiguration.Factory(new FactoryContext
					{
						container = this,
						contracts = service.Context.RequiredContractNames()
					}));
					service.EndResolveDependencies();
					return;
				}
				implementationTypes = interfaceConfiguration.ImplementationTypes;
				useAutosearch = interfaceConfiguration.UseAutosearch;
			}
			if (service.Type.IsValueType)
				service.Throw("can't create value type");
			if (factoryPlugins.Any(p => p.TryInstantiate(this, service)))
			{
				service.SetDependencies(new List<ContainerService>());
				service.EndResolveDependencies();
				return;
			}
			if (service.Type.IsGenericType && service.Type.ContainsGenericParameters)
			{
				service.Context.Report("has open generic arguments");
				service.EndResolveDependencies();
				return;
			}
			if (service.Type.IsAbstract)
				InstantiateInterface(service, implementationTypes, useAutosearch);
			else
				InstantiateImplementation(service);
		}

		private void InstantiateInterface(ContainerService service, IEnumerable<Type> implementationTypes, bool useAutosearch)
		{
			IEnumerable<Type> localTypes;
			if (implementationTypes == null)
				localTypes = GetInheritors(service.Type);
			else if (useAutosearch)
				localTypes = implementationTypes.Union(GetInheritors(service.Type));
			else
				localTypes = implementationTypes;
			var localTypesArray = localTypes.ToArray();
			if (localTypesArray.Length == 0)
			{
				service.Context.Report("has no implementations");
				service.EndResolveDependencies();
				return;
			}
			foreach (var implementationType in localTypesArray)
			{
				ContainerService childService;
				if (service.CreateNew)
				{
					childService = new ContainerService(implementationType).WithArguments(service.Arguments);
					service.Context.Instantiate(null, childService, this);
				}
				else
					childService = ResolveSingleton(implementationType, null, service.Context);
				service.UnionFrom(childService);
			}
			service.EndResolveDependencies();
		}

		private void InstantiateImplementation(ContainerService service)
		{
			if (service.Type.IsDefined<IgnoreImplementationAttribute>())
			{
				service.EndResolveDependencies();
				return;
			}
			var implementationConfiguration = service.Context.GetConfiguration<ImplementationConfiguration>(service.Type);
			if (implementationConfiguration != null && implementationConfiguration.DontUseIt)
			{
				service.EndResolveDependencies();
				return;
			}
			var factoryMethod = GetFactoryOrNull(service.Type);
			if (factoryMethod == null)
				DefaultInstantiateImplementation(service.Type, service);
			else
			{
				var factory = ResolveSingleton(factoryMethod.DeclaringType, null, service.Context);
				if (factory.Instances.Count == 1)
					InvokeConstructor(factoryMethod, factory.Instances[0], new object[0], service);
				service.EndResolveDependencies();
			}
			if (implementationConfiguration != null && implementationConfiguration.InstanceFilter != null)
			{
				service.FilterInstances(implementationConfiguration.InstanceFilter);
				service.Context.Report("instance filter");
			}
		}

		private static MethodInfo GetFactoryOrNull(Type type)
		{
			var factoryType = type.GetNestedType("Factory");
			return factoryType == null ? null : factoryType.GetMethod("Create", Type.EmptyTypes);
		}

		private IEnumerable<Type> GetInheritors(Type type)
		{
			return type.IsAbstract ? inheritors.GetOrNull(type).EmptyIfNull() : EnumerableHelpers.Return(type);
		}

		public IEnumerable<Type> GetDependencies(Type type)
		{
			EnsureNotDisposed();
			if (typeof (Delegate).IsAssignableFrom(type))
				return Enumerable.Empty<Type>();
			if (!type.IsAbstract)
			{
				var result = dependenciesInjector.GetDependencies(type)
					.Select(UnwrapEnumerable)
					.ToArray();
				if (result.Any())
					return result;
			}
			var implementation = new Implementation(type);
			ConstructorInfo constructor;
			if (!implementation.TryGetConstructor(out constructor))
				return Enumerable.Empty<Type>();
			implementation.SetConfiguration(configuration);
			return constructor.GetParameters()
				.Where(p => implementation.GetDependencyConfiguration(p) == null)
				.Select(x => x.ParameterType)
				.Select(UnwrapEnumerable)
				.Where(p => configuration.GetOrNull<object>(p) == null)
				.Where(IsDependency);
		}

		private static bool IsDependency(Type type)
		{
			if (typeof (Delegate).IsAssignableFrom(type))
				return false;
			if (ReflectionHelpers.simpleTypes.Contains(type))
				return false;
			if (type.IsArray && ReflectionHelpers.simpleTypes.Contains(type.GetElementType()))
				return false;
			return true;
		}

		private class Implementation
		{
			public readonly ConstructorInfo[] publicConstructors;
			public readonly Type type;
			private ImplementationConfiguration implementationConfiguration;
			private ImplementationConfiguration definitionConfiguration;

			public Implementation(Type type)
			{
				this.type = type;
				publicConstructors = type.GetConstructors().Where(x => x.IsPublic).ToArray();
			}

			public bool TryGetConstructor(out ConstructorInfo constructor)
			{
				return publicConstructors.SafeTrySingle(out constructor) ||
				       publicConstructors.SafeTrySingle(x => x.IsDefined<ContainerConstructorAttribute>(), out constructor);
			}

			public void SetConfiguration(IContainerConfiguration configuration)
			{
				implementationConfiguration = configuration.GetOrNull<ImplementationConfiguration>(type);
				definitionConfiguration = type.IsGenericType
					? configuration.GetOrNull<ImplementationConfiguration>(type.GetGenericTypeDefinition())
					: null;
			}

			public void SetContext(ResolutionContext context)
			{
				implementationConfiguration = context.GetConfiguration<ImplementationConfiguration>(type);
				definitionConfiguration = type.IsGenericType
					? context.GetConfiguration<ImplementationConfiguration>(type.GetGenericTypeDefinition())
					: null;
			}

			public ImplentationDependencyConfiguration GetDependencyConfiguration(ParameterInfo formalParameter)
			{
				ImplentationDependencyConfiguration dependencyConfiguration = null;
				if (implementationConfiguration != null)
					dependencyConfiguration = implementationConfiguration.GetOrNull(formalParameter);
				if (dependencyConfiguration == null && definitionConfiguration != null)
					dependencyConfiguration = definitionConfiguration.GetOrNull(formalParameter);
				return dependencyConfiguration;
			}

			public IEnumerable<string> GetUnusedDependencyConfigurationNames()
			{
				return implementationConfiguration != null
					? implementationConfiguration.GetUnusedDependencyConfigurationKeys()
					: Enumerable.Empty<string>();
			}
		}

		private void DefaultInstantiateImplementation(Type type, ContainerService service)
		{
			var implementation = new Implementation(type);
			ConstructorInfo constructor;
			if (!implementation.TryGetConstructor(out constructor))
				service.Throw(implementation.publicConstructors.Length == 0
					? "no public ctors, maybe ctor is private?"
					: "many public ctors, maybe some of them should be made private?");
			implementation.SetContext(service.Context);
			var formalParameters = constructor.GetParameters();
			var actualArguments = new object[formalParameters.Length];
			var dependencies = formalParameters.Length > 0 ? new List<ContainerService>() : null;
			for (var i = 0; i < formalParameters.Length; i++)
			{
				var formalParameter = formalParameters[i];
				var dependency = InstantiateDependency(formalParameter, implementation, service);
				service.UnionUsedContracts(dependency);
				if (dependency.Instances.Count == 0)
				{
					service.EndResolveDependencies();
					return;
				}
				dependencies.Add(dependency);
				var dependencyValue = dependency.SingleInstance(false);
				var castedValue = ImplicitTypeCaster.TryCast(dependencyValue, formalParameter.ParameterType);
				if (dependencyValue != null && castedValue == null)
					service.Throw("can't cast [{0}] to [{1}] for dependency [{2}] with value [{3}]",
						dependencyValue.GetType().FormatName(),
						formalParameter.ParameterType.FormatName(),
						formalParameter.Name,
						dependencyValue);
				actualArguments[i] = castedValue;
			}
			service.SetDependencies(dependencies);
			service.EndResolveDependencies();
			var unusedDependencyConfigurations = implementation.GetUnusedDependencyConfigurationNames().ToArray();
			if (unusedDependencyConfigurations.Length > 0)
				service.Throw("unused dependency configurations [{0}]", unusedDependencyConfigurations.JoinStrings(","));
			if (service.FinalUsedContracts.Count == service.Context.requiredContracts.Count)
			{
				InvokeConstructor(constructor, null, actualArguments, service);
				return;
			}
			var usedContactsCacheKey = new CacheKey(type, service.FinalUsedContracts);
			var serviceForUsedContracts = instanceCache.GetOrAdd(usedContactsCacheKey, createContainerServiceDelegate);
			if (serviceForUsedContracts.AcquireInstantiateLock())
				try
				{
					InvokeConstructor(constructor, null, actualArguments, serviceForUsedContracts);
					serviceForUsedContracts.AttachToContext(service.Context);
					serviceForUsedContracts.UnionUsedContracts(service);
					serviceForUsedContracts.SetDependencies(dependencies);
					serviceForUsedContracts.EndResolveDependencies();
					serviceForUsedContracts.InstantiatedSuccessfully(Interlocked.Increment(ref topSortIndex));
				}
				catch
				{
					serviceForUsedContracts.InstantiatedUnsuccessfully();
					throw;
				}
				finally
				{
					serviceForUsedContracts.ReleaseInstantiateLock();
				}
			else
				service.Context.Report("reused");
			foreach (var instance in serviceForUsedContracts.Instances)
				service.AddInstance(instance);
		}

		private static ContainerService IndependentService(object instance)
		{
			var result = new ContainerService(null);
			result.AddInstance(instance);
			return result;
		}

		private static ContainerService DependentService(object instance, ContainerService dependency)
		{
			var result = new ContainerService(null);
			result.AttachToContext(dependency.Context);
			result.AddInstance(instance);
			result.UnionUsedContracts(dependency);
			return result;
		}

		private static List<string> GetContracts(ParameterInfo formalParameter, Type dependencyType)
		{
			var parameterContract = formalParameter.GetCustomAttributeOrNull<RequireContractAttribute>();
			var dependencyTypeContract = dependencyType.GetCustomAttributeOrNull<RequireContractAttribute>();
			if (parameterContract == null && dependencyTypeContract == null)
				return null;
			var result = new List<string>();
			if (parameterContract != null)
				result.Add(parameterContract.ContractName);
			if (dependencyTypeContract != null)
				result.Add(dependencyTypeContract.ContractName);
			return result;
		}

		private ContainerService InstantiateDependency(ParameterInfo formalParameter, Implementation implementation,
			ContainerService service)
		{
			object actualArgument;
			if (service.Arguments != null && service.Arguments.TryGet(formalParameter.Name, out actualArgument))
				return IndependentService(actualArgument);
			var dependencyConfiguration = implementation.GetDependencyConfiguration(formalParameter);
			Type implementationType = null;
			if (dependencyConfiguration != null)
			{
				if (dependencyConfiguration.ValueAssigned)
					return IndependentService(dependencyConfiguration.Value);
				if (dependencyConfiguration.Factory != null)
					return IndependentService(dependencyConfiguration.Factory(this));
				implementationType = dependencyConfiguration.ImplementationType;
			}
			implementationType = implementationType ?? formalParameter.ParameterType;
			if (ReflectionHelpers.simpleTypes.Contains(implementationType) && formalParameter.HasDefaultValue)
				return IndependentService(formalParameter.DefaultValue);
			FromResourceAttribute resourceAttribute;
			if (implementationType == typeof (Stream) && formalParameter.TryGetCustomAttribute(out resourceAttribute))
			{
				var resourceStream = implementation.type.Assembly.GetManifestResourceStream(implementation.type,
					resourceAttribute.Name);
				if (resourceStream == null)
					throw new SimpleContainerException(
						string.Format("can't find resource [{0}] in namespace of [{1}], assembly [{2}]\r\n{3}",
							resourceAttribute.Name, implementation.type,
							implementation.type.Assembly.GetName().Name, service.Context.Format()));
				return IndependentService(resourceStream);
			}
			Type enumerableItem;
			var isEnumerable = TryUnwrapEnumerable(implementationType, out enumerableItem);
			var dependencyType = isEnumerable ? enumerableItem : implementationType;
			var contracts = GetContracts(formalParameter, dependencyType);
			var interfaceConfiguration = service.Context.GetConfiguration<InterfaceConfiguration>(implementationType);
			if (interfaceConfiguration != null && interfaceConfiguration.Factory != null)
			{
				var requiredContracts = new List<string>(service.Context.RequiredContractNames());
				if (contracts != null)
					requiredContracts.AddRange(contracts);
				var instance = interfaceConfiguration.Factory(new FactoryContext
				{
					container = this,
					target = implementation.type,
					contracts = requiredContracts
				});
				return IndependentService(instance);
			}
			var result = service.Context.Resolve(dependencyType, formalParameter.Name, this, contracts);
			if (isEnumerable)
				return DependentService(result.AsEnumerable(), result);
			if (result.Instances.Count == 0)
			{
				if (formalParameter.HasDefaultValue)
					return IndependentService(formalParameter.DefaultValue);
				if (formalParameter.IsDefined<OptionalAttribute>())
					return IndependentService(null);
			}
			return result;
		}

		private static bool TryUnwrapEnumerable(Type type, out Type result)
		{
			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof (IEnumerable<>))
			{
				result = type.GetGenericArguments()[0];
				return true;
			}
			result = null;
			return false;
		}

		private static Type UnwrapEnumerable(Type type)
		{
			Type result;
			return TryUnwrapEnumerable(type, out result) ? result : type;
		}

		private struct CacheKey : IEquatable<CacheKey>
		{
			public readonly Type type;
			public readonly List<string> contracts;
			public readonly string contractsKey;

			public CacheKey(Type type, List<string> contracts)
			{
				this.type = type;
				this.contracts = contracts ?? new List<string>(0);
				contractsKey = InternalHelpers.FormatContractsKey(this.contracts);
			}

			public bool Equals(CacheKey other)
			{
				return type == other.type && contractsKey == other.contractsKey;
			}

			public override bool Equals(object obj)
			{
				if (ReferenceEquals(null, obj)) return false;
				return obj is CacheKey && Equals((CacheKey) obj);
			}

			public override int GetHashCode()
			{
				unchecked
				{
					return (type.GetHashCode()*397) ^ (contractsKey == null ? 0 : contractsKey.GetHashCode());
				}
			}

			public static bool operator ==(CacheKey left, CacheKey right)
			{
				return left.Equals(right);
			}

			public static bool operator !=(CacheKey left, CacheKey right)
			{
				return !left.Equals(right);
			}
		}

		private static void InvokeConstructor(MethodBase method, object self, object[] actualArguments,
			ContainerService containerService)
		{
			try
			{
				var factoryMethod = compiledMethods.GetOrAdd(method, compileMethodDelegate);
				var instance = factoryMethod(self, actualArguments);
				containerService.AddInstance(instance);
			}
			catch (ServiceCouldNotBeCreatedException)
			{
			}
			catch (Exception e)
			{
				throw new SimpleContainerException("construction exception\r\n" + containerService.Context.Format() + "\r\n", e);
			}
		}

		public void Dispose()
		{
			if (disposed)
				return;
			var servicesToDispose = GetInstanceCache(typeof (IDisposable))
				.Select(x => x.Cast<IDisposable>())
				.Where(x => !ReferenceEquals(x.Instance, this))
				.Reverse()
				.ToArray();
			disposed = true;
			var exceptions = new List<SimpleContainerException>();
			foreach (var disposable in servicesToDispose)
			{
				try
				{
					DisposeService(disposable);
				}
				catch (SimpleContainerException e)
				{
					exceptions.Add(e);
				}
			}
			if (exceptions.Count > 0)
				throw new AggregateException("error disposing services", exceptions);
		}

		private static void DisposeService(ServiceInstance<IDisposable> disposable)
		{
			try
			{
				disposable.Instance.Dispose();
			}
			catch (Exception e)
			{
				var message = string.Format("error disposing [{0}]", disposable.FormatName());
				throw new SimpleContainerException(message, e);
			}
		}

		protected void EnsureNotDisposed()
		{
			if (disposed)
				throw new ObjectDisposedException("SimpleContainer");
		}
	}
}