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
				lock (context.locker)
					return ResolveSingleton(key.type, null, context);
			};
		}

		public object Get(Type serviceType, IEnumerable<string> contracts)
		{
			EnsureNotDisposed();
			Type enumerableItem;
			if (TryUnwrapEnumerable(serviceType, out enumerableItem))
				return GetAll(enumerableItem);
			var targetContracts = contracts;
			RequireContractAttribute requireContractAttribute;
			if (serviceType.TryGetCustomAttribute(out requireContractAttribute))
				targetContracts = targetContracts.Concat(requireContractAttribute.ContractName);
			return GetInternal(new CacheKey(serviceType, targetContracts)).SingleInstance();
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

		internal object Create(Type type, IEnumerable<string> contracts, object arguments, ResolutionContext resolutionContext)
		{
			EnsureNotDisposed();
			resolutionContext = resolutionContext ?? new ResolutionContext(configuration, contracts);
			lock (resolutionContext.locker)
			{
				var result = ContainerService.ForFactory(type, arguments);
				resolutionContext.Instantiate(null, result, this);
				if (result.Arguments != null)
				{
					var unused = result.Arguments.GetUnused().ToArray();
					if (unused.Any())
						resolutionContext.Throw("arguments [{0}] are not used", unused.JoinStrings(","));
				}
				return result.SingleInstance();
			}
		}

		public object Create(Type type, IEnumerable<string> contracts, object arguments)
		{
			return Create(type, contracts, arguments, null);
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
			var cacheKey = new CacheKey(type, contracts);
			if (!instanceCache.TryGetValue(cacheKey, out containerService)) return;
			if (entireResolutionContext)
				containerService.Context.Format(null, null, writer);
			else
				containerService.Context.Format(type, cacheKey.contractsKey, writer);
		}

		public IEnumerable<object> GetInstanceCache(Type type)
		{
			EnsureNotDisposed();
			var resultServices = instanceCache.Values
				.Where(x => x.WaitForSuccessfullResolve() && !x.Type.IsAbstract && type.IsAssignableFrom(x.Type))
				.ToArray();
			var processedContexts = new HashSet<ResolutionContext>();
			var reachableServices = new HashSet<ContainerService>();
			foreach (var containerService in resultServices)
				if (processedContexts.Add(containerService.Context))
					containerService.Context.Mark(reachableServices);
			var result = new List<ContainerService>(resultServices.Where(reachableServices.Contains));
			result.Sort((a, b) => a.TopSortIndex.CompareTo(b.TopSortIndex));
			return result.SelectMany(x => x.Instances).Distinct().ToArray();
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
			var serviceCacheLevel = GetCacheLevel(type);
			if (serviceCacheLevel == CacheLevel.Static && cacheLevel == CacheLevel.Local)
				return staticContainer.ResolveSingleton(type, name, context);
			if (serviceCacheLevel == CacheLevel.Local && cacheLevel == CacheLevel.Static)
				context.Throw("local service [{0}] can't be resolved in static context", type.FormatName());
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
					return;
				}
				if (interfaceConfiguration.Factory != null)
				{
					service.AddInstance(interfaceConfiguration.Factory(new FactoryContext
					{
						container = this,
						contracts = service.Context.RequiredContractNames()
					}));
					return;
				}
				implementationTypes = interfaceConfiguration.ImplementationTypes;
				useAutosearch = interfaceConfiguration.UseAutosearch;
			}
			if (service.Type.IsValueType)
				service.Throw("can't create value type");
			if (factoryPlugins.Any(p => p.TryInstantiate(this, service)))
				return;
			if (service.Type.IsGenericType && service.Type.ContainsGenericParameters)
			{
				service.Context.Report("has open generic arguments");
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
				return;
			var implementationConfiguration = service.Context.GetConfiguration<ImplementationConfiguration>(service.Type);
			if (implementationConfiguration != null && implementationConfiguration.DontUseIt)
				return;
			var factoryMethod = GetFactoryOrNull(service.Type);
			if (factoryMethod == null)
				DefaultInstantiateImplementation(service.Type, service);
			else
			{
				var factory = ResolveSingleton(factoryMethod.DeclaringType, null, service.Context);
				if (factory.Instances.Count == 1)
				{
					var instance = InvokeConstructor(factoryMethod, factory.Instances[0], new object[0], service.Context);
					service.AddInstance(instance);
				}
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
				var dependencyValue = dependency.SingleInstance();
				var castedValue = ImplicitTypeCaster.TryCast(dependencyValue, formalParameter.ParameterType);
				if (dependencyValue != null && castedValue == null)
					service.Throw("can't cast [{0}] to [{1}] for dependency [{2}] with value [{3}]",
						dependencyValue.GetType().FormatName(),
						formalParameter.ParameterType.FormatName(),
						formalParameter.Name,
						dependencyValue);
				actualArguments[i] = castedValue;
			}
			service.EndResolveDependencies();
			var unusedDependencyConfigurations = implementation.GetUnusedDependencyConfigurationNames().ToArray();
			if (unusedDependencyConfigurations.Length > 0)
				service.Throw("unused dependency configurations [{0}]", unusedDependencyConfigurations.JoinStrings(","));
			if (service.FinalUsedContracts.Length == service.Context.requiredContracts.Count)
			{
				var instance = InvokeConstructor(constructor, null, actualArguments, service.Context);
				service.AddInstance(instance);
				return;
			}
			var usedContactsCacheKey = new CacheKey(type, service.FinalUsedContracts);
			var serviceForUsedContracts = instanceCache.GetOrAdd(usedContactsCacheKey, createContainerServiceDelegate);
			if (serviceForUsedContracts.AcquireInstantiateLock())
				try
				{
					var instance = InvokeConstructor(constructor, null, actualArguments, service.Context);
					serviceForUsedContracts.AttachToContext(service.Context);
					serviceForUsedContracts.UnionUsedContracts(service);
					serviceForUsedContracts.AddInstance(instance);
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
			result.AddInstance(instance);
			result.UnionUsedContracts(dependency);
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


			var parameterContract = formalParameter.GetCustomAttributeOrNull<RequireContractAttribute>();
			var dependencyTypeContract = dependencyType.GetCustomAttributeOrNull<RequireContractAttribute>();

			var interfaceConfiguration = service.Context.GetConfiguration<InterfaceConfiguration>(implementationType);
			if (interfaceConfiguration != null && interfaceConfiguration.Factory != null)
			{
				var contacts = new List<string>(service.Context.RequiredContractNames());
				if (parameterContract != null)
					contacts.Add(parameterContract.ContractName);
				if (dependencyTypeContract != null)
					contacts.Add(dependencyTypeContract.ContractName);
				var instance = interfaceConfiguration.Factory(new FactoryContext
				{
					container = this,
					target = implementation.type,
					contracts = contacts
				});
				return IndependentService(instance);
			}

			ContainerService result;
			if (parameterContract != null || dependencyTypeContract != null)
			{
				var contractServices = service.Context.ResolveUsingContract(dependencyType, formalParameter.Name,
					parameterContract == null ? null : parameterContract.ContractName,
					dependencyTypeContract == null ? null : dependencyTypeContract.ContractName, this);
				result = new ContainerService(dependencyType);
				result.AttachToContext(service.Context);
				foreach (var contractService in contractServices)
					result.UnionFrom(contractService);
			}
			else
				result = ResolveSingleton(dependencyType, formalParameter.Name, service.Context);
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
			public readonly string[] contracts;
			public readonly string contractsKey;

			public CacheKey(Type type, IEnumerable<string> contracts)
			{
				this.type = type;
				this.contracts = (contracts ?? new string[0]).ToArray();
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
					return (type.GetHashCode()*397) ^ contractsKey.GetHashCode();
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

		private static object InvokeConstructor(MethodBase method, object self, object[] actualArguments,
			ResolutionContext resolutionContext)
		{
			try
			{
				var compiledMethod = compiledMethods.GetOrAdd(method, compileMethodDelegate);
				return compiledMethod(self, actualArguments);
			}
			catch (Exception e)
			{
				throw new SimpleContainerException("construction exception\r\n" + resolutionContext.Format() + "\r\n", e);
			}
		}

		public void Dispose()
		{
			if (disposed)
				return;
			var servicesToDispose = this.GetInstanceCache<IDisposable>()
				.Where(x => !ReferenceEquals(x, this))
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

		private static void DisposeService(IDisposable disposable)
		{
			try
			{
				disposable.Dispose();
			}
			catch (Exception e)
			{
				var message = string.Format("error disposing [{0}]", disposable.GetType().FormatName());
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