using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using SimpleContainer.Configuration;
using SimpleContainer.Factories;
using SimpleContainer.Generics;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class SimpleContainer : IContainer
	{
		private static readonly Func<CacheKey, ContainerService> createContainerServiceDelegate =
			k => new ContainerService(k.type);

		private static readonly IFactoryPlugin[] factoryPlugins =
		{
			new SimpleFactoryPlugin(),
			new FactoryWithArgumentsPlugin(),
			new LazyFactoryPlugin()
		};

		protected readonly IContainerConfiguration configuration;
		protected readonly IInheritanceHierarchy inheritors;
		private readonly StaticContainer staticContainer;
		internal readonly CacheLevel cacheLevel;
		protected readonly LogError errorLogger;
		protected readonly LogInfo infoLogger;
		protected readonly ISet<Type> staticServices;

		private readonly ConcurrentDictionary<CacheKey, ContainerService> instanceCache =
			new ConcurrentDictionary<CacheKey, ContainerService>();

		private readonly Func<CacheKey, ContainerService> createInstanceDelegate;
		private readonly DependenciesInjector dependenciesInjector;
		private int topSortIndex;
		private bool disposed;
		private readonly ComponentsRunner componentsRunner;

		public SimpleContainer(IContainerConfiguration configuration, IInheritanceHierarchy inheritors,
			StaticContainer staticContainer, CacheLevel cacheLevel, ISet<Type> staticServices, LogError errorLogger,
			LogInfo infoLogger)
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
			this.staticServices = staticServices;
			this.errorLogger = errorLogger;
			this.infoLogger = infoLogger;
			componentsRunner = new ComponentsRunner(infoLogger);
		}

		public ResolvedService Resolve(Type type, IEnumerable<string> contracts)
		{
			EnsureNotDisposed();
			if (type == null)
				throw new ArgumentNullException("type");
			var contractsArray = CheckContracts(contracts);

			Type enumerableItem;
			var isEnumerable = TryUnwrapEnumerable(type, out enumerableItem);
			var typeToResolve = isEnumerable ? enumerableItem : type;
			var cacheKey = new CacheKey(typeToResolve, InternalHelpers.ToInternalContracts(contractsArray, typeToResolve));
			var result = instanceCache.GetOrAdd(cacheKey, createInstanceDelegate);
			result.WaitForResolveOrDie();
			return new ResolvedService(result, this, isEnumerable);
		}

		internal ContainerService Create(Type type, IEnumerable<string> contracts, object arguments, ResolutionContext context)
		{
			context = context ?? new ResolutionContext(configuration, InternalHelpers.ToInternalContracts(contracts, type));
			var result = ContainerService.ForFactory(type, arguments);
			context.Instantiate(result, null, this);
			if (result.Arguments != null)
			{
				var unused = result.Arguments.GetUnused().ToArray();
				if (unused.Any())
					result.EndResolveDependenciesWithFailure(string.Format("arguments [{0}] are not used", unused.JoinStrings(",")));
			}
			return result;
		}

		private static string[] CheckContracts(IEnumerable<string> contracts)
		{
			if (contracts == null)
				return null;
			var contractsArray = contracts.ToArray();
			foreach (var contract in contractsArray)
				if (string.IsNullOrEmpty(contract))
				{
					var message = string.Format("invalid contracts [{0}]", contractsArray.Select(x => x ?? "<null>").JoinStrings(","));
					throw new SimpleContainerException(message);
				}
			return contractsArray;
		}

		public ResolvedService Create(Type type, IEnumerable<string> contracts, object arguments)
		{
			EnsureNotDisposed();
			if (type == null)
				throw new ArgumentNullException("type");
			var contractsArray = CheckContracts(contracts);

			var result = Create(type, contractsArray, arguments, null);
			result.CheckOk();
			return new ResolvedService(result, this, false);
		}

		internal void Run(ContainerService containerService, string constructionLog)
		{
			if (constructionLog != null && infoLogger != null)
				infoLogger(new ServiceName(containerService.Type, containerService.FinalUsedContracts), "\r\n" + constructionLog);
			containerService.EnsureRunCalled(componentsRunner, true);
		}

		public IEnumerable<Type> GetImplementationsOf(Type interfaceType)
		{
			EnsureNotDisposed();
			if (interfaceType == null)
				throw new ArgumentNullException("interfaceType");

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

		public BuiltUpService BuildUp(object target, IEnumerable<string> contracts)
		{
			EnsureNotDisposed();
			if (target == null)
				throw new ArgumentNullException("target");
			var contractsArray = CheckContracts(contracts);

			return dependenciesInjector.BuildUp(target, contractsArray);
		}

		private IEnumerable<ServiceInstance> GetInstanceCache(Type interfaceType)
		{
			var result = instanceCache.Values
				.Where(x => x.WaitForResolve() && !x.Type.IsAbstract && interfaceType.IsAssignableFrom(x.Type))
				.ToList();
			result.Sort((a, b) => a.TopSortIndex.CompareTo(b.TopSortIndex));
			return result.SelectMany(x => x.GetInstances()).Distinct(new ServiceInstanceEqualityComparer()).ToArray();
		}

		private class ServiceInstanceEqualityComparer : IEqualityComparer<ServiceInstance>
		{
			public bool Equals(ServiceInstance x, ServiceInstance y)
			{
				return ReferenceEquals(x.Instance, y.Instance);
			}

			public int GetHashCode(ServiceInstance obj)
			{
				return obj.Instance.GetHashCode();
			}
		}

		public IContainer Clone(Action<ContainerConfigurationBuilder> configure)
		{
			EnsureNotDisposed();
			return new SimpleContainer(CloneConfiguration(configure), inheritors, staticContainer,
				cacheLevel, staticServices, null, infoLogger);
		}

		protected IContainerConfiguration CloneConfiguration(Action<ContainerConfigurationBuilder> configure)
		{
			if (configure == null)
				return configuration;
			var builder = new ContainerConfigurationBuilder(staticServices, cacheLevel == CacheLevel.Static);
			configure(builder);
			return new MergedConfiguration(configuration, builder.Build());
		}

		internal virtual CacheLevel GetCacheLevel(Type type)
		{
			return staticServices.Contains(type) || type.IsDefined<StaticAttribute>() ? CacheLevel.Static : CacheLevel.Local;
		}

		internal ContainerService ResolveSingleton(Type type, string name, ResolutionContext context)
		{
			if (cacheLevel == CacheLevel.Local && GetCacheLevel(type) == CacheLevel.Static)
				return staticContainer.ResolveSingleton(type, name, context);
			var cacheKey = new CacheKey(type, context.DeclaredContractNames());
			var result = instanceCache.GetOrAdd(cacheKey, createContainerServiceDelegate);
			if (result.AcquireInstantiateLock())
				try
				{
					context.Instantiate(result, name, this);
					result.InstantiatedSuccessfully(Interlocked.Increment(ref topSortIndex));
				}
				finally
				{
					result.ReleaseInstantiateLock();
				}
			return result;
		}

		internal void Instantiate(ContainerService service)
		{
			if (service.Type.IsSimpleType())
			{
				service.EndResolveDependenciesWithFailure("can't create simple type");
				return;
			}
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
						contracts = service.Context.DeclaredContractNames()
					}));
					service.EndResolveDependencies();
					return;
				}
				implementationTypes = interfaceConfiguration.ImplementationTypes;
				useAutosearch = interfaceConfiguration.UseAutosearch;
			}
			if (service.Type.IsValueType)
			{
				service.EndResolveDependenciesWithFailure("can't create value type");
				return;
			}
			if (factoryPlugins.Any(p => p.TryInstantiate(this, service)))
			{
				service.EndResolveDependencies();
				return;
			}
			if (service.Type.IsGenericType && service.Type.ContainsGenericParameters)
			{
				service.EndResolveDependenciesWithFailure("can't create open generic");
				return;
			}
			if (service.Type.IsAbstract)
				InstantiateInterface(service, implementationTypes, useAutosearch);
			else
				InstantiateImplementation(service);
		}

		private void InstantiateInterface(ContainerService service, IEnumerable<Type> implementationTypes, bool useAutosearch)
		{
			var localTypes = implementationTypes == null || useAutosearch
				? implementationTypes.EmptyIfNull()
					.Union(inheritors.GetOrNull(service.Type.GetDefinition()).EmptyIfNull())
				: implementationTypes;
			var localTypesArray = ProcessGenerics(service.Type, localTypes).ToArray();
			if (localTypesArray.Length == 0)
			{
				service.Context.Comment("has no implementations");
				service.EndResolveDependencies();
				return;
			}
			foreach (var implementationType in localTypesArray)
			{
				ContainerService childService;
				if (service.CreateNew)
				{
					childService = new ContainerService(implementationType).WithArguments(service.Arguments);
					service.Context.Instantiate(childService, null, this);
				}
				else
					childService = service.Context.Resolve(implementationType, null, null, this);
				service.UnionFrom(childService, false);
				if (service.status == ServiceStatus.Failed)
				{
					service.EndResolveDependenciesWithFailure(null);
					return;
				}
			}
			service.EndResolveDependencies();
		}

		private void InstantiateImplementation(ContainerService service)
		{
			if (service.Type.IsDefined("IgnoredImplementationAttribute"))
			{
				service.Context.Comment("IgnoredImplementation");
				service.EndResolveDependencies();
				return;
			}
			var implementationConfiguration = service.Context.GetConfiguration<ImplementationConfiguration>(service.Type);
			if (implementationConfiguration != null && implementationConfiguration.DontUseIt)
			{
				service.message = "DontUse";
				service.EndResolveDependencies();
				return;
			}
			var factoryMethod = GetFactoryOrNull(service.Type);
			if (factoryMethod == null)
				DefaultInstantiateImplementation(service);
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
				service.Context.Comment("instance filter");
			}
		}

		private static MethodInfo GetFactoryOrNull(Type type)
		{
			var factoryType = type.GetNestedType("Factory");
			return factoryType == null ? null : factoryType.GetMethod("Create", Type.EmptyTypes);
		}

		private static IEnumerable<Type> ProcessGenerics(Type type, IEnumerable<Type> candidates)
		{
			foreach (var candidate in candidates)
			{
				if (!candidate.IsGenericType)
				{
					if (!type.IsGenericType || type.IsAssignableFrom(candidate))
						yield return candidate;
					continue;
				}
				if (!candidate.ContainsGenericParameters)
				{
					yield return candidate;
					continue;
				}
				var argumentsCount = candidate.GetGenericArguments().Length;
				var candidateInterfaces = type.IsInterface
					? candidate.GetInterfaces()
					: (type.IsAbstract ? candidate.ParentsOrSelf() : Enumerable.Repeat(candidate, 1));
				foreach (var candidateInterface in candidateInterfaces)
				{
					var arguments = new Type[argumentsCount];
					if (candidateInterface.MatchWith(type, arguments) && arguments.All(x => x != null))
						yield return candidate.MakeGenericType(arguments);
				}
			}
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
			if (type.IsSimpleType())
				return false;
			if (type.IsArray && type.GetElementType().IsSimpleType())
				return false;
			return true;
		}

		private void DefaultInstantiateImplementation(ContainerService service)
		{
			var implementation = new Implementation(service.Type);
			ConstructorInfo constructor;
			if (!implementation.TryGetConstructor(out constructor))
			{
				service.EndResolveDependenciesWithFailure(implementation.publicConstructors.Length == 0
					? "no public ctors, maybe ctor is private?"
					: "many public ctors, maybe some of them should be made private?");
				return;
			}
			implementation.SetService(service);
			var formalParameters = constructor.GetParameters();
			var actualArguments = new object[formalParameters.Length];
			for (var i = 0; i < formalParameters.Length; i++)
			{
				var formalParameter = formalParameters[i];
				var dependency = InstantiateDependency(formalParameter, implementation, service.Context);
				service.AddDependency(dependency);
				object dependencyValue;
				if (dependency.service == null)
				{
					dependencyValue = dependency.Value;
				}
				else
				{
					
					if (dependency.service.status == ServiceStatus.Failed)
					{
						service.EndResolveDependenciesWithFailure(null);
						return;
					}
					service.UnionUsedContracts(dependency.service);
					if (dependency.IsEnumerable)
						dependencyValue = dependency.service.AsEnumerable();
					else if (dependency.service.Instances.Count == 0)
					{
						service.EndResolveDependencies();
						return;
					}
					else
					{
						if (dependency.service.Instances.Count != 1)
						{
							service.EndResolveDependenciesWithFailure(dependency.service.SingleInstanceViolationMessage());
							return;
						}
						dependencyValue = dependency.service.Instances[0];
					}
				}
				var castedValue = ImplicitTypeCaster.TryCast(dependencyValue, formalParameter.ParameterType);
				if (dependencyValue != null && castedValue == null)
				{
					service.EndResolveDependenciesWithFailure(string.Format("can't cast value [{0}] from [{1}] to [{2}] for dependency [{3}]",
						dependencyValue,
						dependencyValue.GetType().FormatName(),
						formalParameter.ParameterType.FormatName(),
						formalParameter.Name));
					return;
				}
				actualArguments[i] = castedValue;
			}
			service.EndResolveDependencies();
			var unusedDependencyConfigurations = implementation.GetUnusedDependencyConfigurationNames().ToArray();
			if (unusedDependencyConfigurations.Length > 0)
			{
				service.EndResolveDependenciesWithFailure(string.Format("unused dependency configurations [{0}]",
					unusedDependencyConfigurations.JoinStrings(",")));
				return;
			}
			if (service.Context.DeclaredContractsCount() == service.FinalUsedContracts.Count)
			{
				InvokeConstructor(constructor, null, actualArguments, service);
				return;
			}
			var usedContactsCacheKey = new CacheKey(service.Type, service.FinalUsedContracts);
			var serviceForUsedContracts = instanceCache.GetOrAdd(usedContactsCacheKey, createContainerServiceDelegate);
			if (serviceForUsedContracts.AcquireInstantiateLock())
				try
				{
					serviceForUsedContracts.AttachToContext(service.Context);
					InvokeConstructor(constructor, null, actualArguments, serviceForUsedContracts);
					serviceForUsedContracts.UnionUsedContracts(service);
					serviceForUsedContracts.UnionDependencies(service);
					serviceForUsedContracts.EndResolveDependencies();
					serviceForUsedContracts.InstantiatedSuccessfully(Interlocked.Increment(ref topSortIndex));
				}
				finally
				{
					serviceForUsedContracts.ReleaseInstantiateLock();
				}
			foreach (var instance in serviceForUsedContracts.Instances)
				service.AddInstance(instance);
		}

		private ServiceDependency CloneWithDefaultValue(ParameterInfo formalParameter, ContainerService containerService, object defaultValue,
			ResolutionContext context)
		{
			var result = new ContainerService(formalParameter.ParameterType);
			result.UnionFrom(containerService, false);
			containerService.AddInstance(defaultValue);
			containerService.name = formalParameter.Name;
			containerService.declaredContracts = context.DeclaredContractNames();
			containerService.isStatic = cacheLevel == CacheLevel.Static;
			return new ServiceDependency {service = containerService};
		}

		private ServiceDependency InstantiateDependency(ParameterInfo formalParameter, Implementation implementation,
			ResolutionContext context)
		{
			object actualArgument;
			if (implementation.Arguments != null && implementation.Arguments.TryGet(formalParameter.Name, out actualArgument))
				return ServiceDependency.Constant(formalParameter, actualArgument);
			var parameters = implementation.GetParameters();
			if (parameters != null && parameters.TryGet(formalParameter.Name, formalParameter.ParameterType, out actualArgument))
				return ServiceDependency.Constant(formalParameter, actualArgument);
			var dependencyConfiguration = implementation.GetDependencyConfiguration(formalParameter);
			Type implementationType = null;
			if (dependencyConfiguration != null)
			{
				if (dependencyConfiguration.ValueAssigned)
					return ServiceDependency.Constant(formalParameter, dependencyConfiguration.Value);
				if (dependencyConfiguration.Factory != null)
					return ServiceDependency.Constant(formalParameter, dependencyConfiguration.Factory(this));
				implementationType = dependencyConfiguration.ImplementationType;
			}
			implementationType = implementationType ?? formalParameter.ParameterType;
			FromResourceAttribute resourceAttribute;
			if (implementationType == typeof (Stream) && formalParameter.TryGetCustomAttribute(out resourceAttribute))
			{
				var resourceStream = implementation.type.Assembly.GetManifestResourceStream(implementation.type,
					resourceAttribute.Name);
				if (resourceStream == null)
					return ServiceDependency.Failed(formalParameter,
						"can't find resource [{0}] in namespace of [{1}], assembly [{2}]",
						resourceAttribute.Name, implementation.type, implementation.type.Assembly.GetName().Name);
				return ServiceDependency.Constant(formalParameter, resourceStream);
			}
			Type enumerableItem;
			var isEnumerable = TryUnwrapEnumerable(implementationType, out enumerableItem);
			var dependencyType = isEnumerable ? enumerableItem : implementationType;
			var attribute = formalParameter.GetCustomAttributeOrNull<RequireContractAttribute>();
			var contracts = attribute == null ? null : new List<string>(1) {attribute.ContractName};
			var interfaceConfiguration = context.GetConfiguration<InterfaceConfiguration>(dependencyType);
			if (interfaceConfiguration != null && interfaceConfiguration.Factory != null)
			{
				var declaredContracts = new List<string>(context.DeclaredContractNames());
				if (contracts != null)
					declaredContracts.AddRange(contracts);
				var instance = interfaceConfiguration.Factory(new FactoryContext
				{
					container = this,
					target = implementation.type,
					contracts = declaredContracts
				});
				return isEnumerable
					? ServiceDependency.Constant(formalParameter, new[] {instance}.CastToArrayOf(dependencyType))
					: ServiceDependency.Constant(formalParameter, instance);
			}
			if (implementationType.IsSimpleType())
			{
				if (!formalParameter.HasDefaultValue)
					return ServiceDependency.Failed(formalParameter,
						"parameter [{0}] of service [{1}] is not configured",
						formalParameter.Name, implementation.type.FormatName());
				return ServiceDependency.Constant(formalParameter, formalParameter.DefaultValue);
			}
			var result = context.Resolve(dependencyType, contracts, formalParameter.Name, this);
			if (isEnumerable)
				return ServiceDependency.Enumerable(result);
			if (result.status == ServiceStatus.Ok && result.Instances.Count == 0)
			{
				if (formalParameter.HasDefaultValue)
					return ServiceDependency.ServiceWithDefaultValue(result, formalParameter.DefaultValue);
				if (IsOptional(formalParameter))
					return ServiceDependency.ServiceWithDefaultValue(result, null);
			}
			return ServiceDependency.Service(result);
		}

		private static bool IsOptional(ICustomAttributeProvider attributes)
		{
			return attributes.IsDefined<OptionalAttribute>() || attributes.IsDefined("CanBeNullAttribute");
		}

		private static bool TryUnwrapEnumerable(Type type, out Type result)
		{
			if (IsEnumerable(type))
			{
				result = type.GetGenericArguments()[0];
				return true;
			}
			if (type.IsArray)
			{
				result = type.GetElementType();
				return true;
			}
			result = null;
			return false;
		}

		private static bool IsEnumerable(Type type)
		{
			return type.IsGenericType && type.GetGenericTypeDefinition() == typeof (IEnumerable<>);
		}

		private static Type UnwrapEnumerable(Type type)
		{
			Type result;
			return TryUnwrapEnumerable(type, out result) ? result : type;
		}

		private static void InvokeConstructor(MethodBase method, object self, object[] actualArguments,
			ContainerService containerService)
		{
			try
			{
				var instance = MethodInvoker.Invoke(method, self, actualArguments);
				containerService.AddInstance(instance);
			}
			catch (ServiceCouldNotBeCreatedException e)
			{
				containerService.message = e.Message;
			}
			catch (Exception e)
			{
				containerService.status = ServiceStatus.Failed;
				containerService.constructionException = e;
			}
		}

		public void Dispose()
		{
			if (disposed)
				return;
			var servicesToDispose = GetInstanceCache(typeof (IDisposable))
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
			{
				var error = new AggregateException("SimpleContainer dispose error", exceptions);
				if (errorLogger == null)
					throw error;
				errorLogger(error.Message, error);
			}
		}

		private static void DisposeService(ServiceInstance disposable)
		{
			try
			{
				((IDisposable) disposable.Instance).Dispose();
			}
			catch (Exception e)
			{
				if (e is OperationCanceledException)
					return;
				var aggregateException = e as AggregateException;
				if (aggregateException != null)
					if (aggregateException.Flatten().InnerExceptions.All(x => x is OperationCanceledException))
						return;
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