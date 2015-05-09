using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
		private readonly Func<ServiceName, ContainerService> createContainerServiceDelegate;

		private static readonly IFactoryPlugin[] factoryPlugins =
		{
			new SimpleFactoryPlugin(),
			new FactoryWithArgumentsPlugin(),
			new LazyFactoryPlugin()
		};

		protected readonly IContainerConfiguration configuration;
		protected readonly IInheritanceHierarchy inheritors;
		private readonly StaticContainer staticContainer;
		protected readonly CacheLevel cacheLevel;
		protected readonly LogError errorLogger;
		protected readonly LogInfo infoLogger;
		protected readonly ISet<Type> staticServices;

		private readonly ConcurrentDictionary<ServiceName, ContainerService> instanceCache =
			new ConcurrentDictionary<ServiceName, ContainerService>();

		private readonly Func<ServiceName, ContainerService> createInstanceDelegate;
		private readonly DependenciesInjector dependenciesInjector;
		private bool disposed;

		public SimpleContainer(IContainerConfiguration configuration, IInheritanceHierarchy inheritors,
			StaticContainer staticContainer, CacheLevel cacheLevel, ISet<Type> staticServices, LogError errorLogger,
			LogInfo infoLogger)
		{
			this.configuration = configuration;
			this.inheritors = inheritors;
			this.staticContainer = staticContainer;
			this.cacheLevel = cacheLevel;
			dependenciesInjector = new DependenciesInjector(this);
			createContainerServiceDelegate = k => NewService(k.Type);
			createInstanceDelegate = delegate(ServiceName key)
			{
				var context = new ResolutionContext(configuration, key.Contracts);
				return ResolveSingleton(key.Type, context);
			};
			this.staticServices = staticServices;
			this.errorLogger = errorLogger;
			this.infoLogger = infoLogger;
		}

		public ResolvedService Resolve(Type type, IEnumerable<string> contracts)
		{
			EnsureNotDisposed();
			if (type == null)
				throw new ArgumentNullException("type");
			var contractsArray = CheckContracts(contracts);
			var typeToResolve = type.UnwrapEnumerable();
			var cacheKey = new ServiceName(typeToResolve, InternalHelpers.ToInternalContracts(contractsArray, typeToResolve));
			var result = instanceCache.GetOrAdd(cacheKey, createInstanceDelegate);
			result.WaitForResolveOrDie();
			return new ResolvedService(result, this, typeToResolve != type);
		}

		internal ContainerService NewService(Type type)
		{
			return new ContainerService(type, cacheLevel);
		}

		internal ContainerService Create(Type type, IEnumerable<string> contracts, object arguments, ResolutionContext context)
		{
			context = context ?? new ResolutionContext(configuration, InternalHelpers.ToInternalContracts(contracts, type));
			var result = NewService(type).ForFactory(ObjectAccessor.Get(arguments), true);
			context.Instantiate(result, this);
			if (result.status.IsGood() && result.Arguments != null)
			{
				var unused = result.Arguments.GetUnused().ToArray();
				if (unused.Any())
					result.SetError(string.Format("arguments [{0}] are not used", unused.JoinStrings(",")));
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
			containerService.EnsureRunCalled(infoLogger);
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

		private IEnumerable<NamedInstance> GetInstanceCache(Type interfaceType)
		{
			var seen = new HashSet<object>();
			var target = new List<NamedInstance>();
			foreach (var service in instanceCache.Values)
				if (service.WaitForResolve())
					service.CollectInstances(interfaceType, cacheLevel, seen, target);
			return target;
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

		internal ContainerService ResolveSingleton(Type type, ResolutionContext context)
		{
			if (cacheLevel == CacheLevel.Local && GetCacheLevel(type) == CacheLevel.Static)
				return staticContainer.ResolveSingleton(type, context);
			var cacheKey = new ServiceName(type, context.DeclaredContractNames());
			var result = instanceCache.GetOrAdd(cacheKey, createContainerServiceDelegate);
			ContainerService cycle;
			if (context.DetectCycle(result, this, out cycle))
				return cycle;
			if (result.AcquireInstantiateLock())
				try
				{
					context.Instantiate(result, this);
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
				service.SetError("can't create simple type");
				return;
			}
			if (service.Type == typeof (IContainer))
			{
				service.AddInstance(this, false);
				return;
			}
			var interfaceConfiguration = service.Context.GetConfiguration<InterfaceConfiguration>(service.Type);
			IEnumerable<Type> implementationTypes = null;
			var useAutosearch = false;
			if (interfaceConfiguration != null)
			{
				if (interfaceConfiguration.ImplementationAssigned)
				{
					service.AddInstance(interfaceConfiguration.Implementation, interfaceConfiguration.ContainerOwnsInstance);
					return;
				}
				if (interfaceConfiguration.Factory != null)
				{
					service.AddInstance(interfaceConfiguration.Factory(new FactoryContext
					{
						container = this,
						contracts = service.Context.DeclaredContractNames()
					}), interfaceConfiguration.ContainerOwnsInstance);
					return;
				}
				implementationTypes = interfaceConfiguration.ImplementationTypes;
				useAutosearch = interfaceConfiguration.UseAutosearch;
			}
			if (service.Type.IsValueType)
			{
				service.SetError("can't create value type");
				return;
			}
			if (factoryPlugins.Any(p => p.TryInstantiate(this, service)))
				return;
			if (service.Type.IsGenericType && service.Type.ContainsGenericParameters)
			{
				service.SetError("can't create open generic");
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
			var localTypesArray = localTypes.MatchWith(service.Type).ToArray();
			if (localTypesArray.Length == 0)
			{
				service.SetComment("has no implementations");
				return;
			}
			foreach (var implementationType in localTypesArray)
			{
				ContainerService childService;
				if (service.CreateNew)
				{
					childService = NewService(implementationType).ForFactory(service.Arguments, false);
					service.Context.Instantiate(childService, this);
				}
				else
					childService = service.Context.Resolve(implementationType, null, this);
				if (!service.LinkTo(childService))
					return;
			}
		}

		private void InstantiateImplementation(ContainerService service)
		{
			if (service.Type.IsDefined("IgnoredImplementationAttribute"))
			{
				service.SetComment("IgnoredImplementation");
				return;
			}
			var implementationConfiguration = service.Context.GetConfiguration<ImplementationConfiguration>(service.Type);
			if (implementationConfiguration != null && implementationConfiguration.DontUseIt)
			{
				service.SetComment("DontUse");
				return;
			}
			var factoryMethod = GetFactoryOrNull(service.Type);
			if (factoryMethod == null)
				DefaultInstantiateImplementation(service);
			else
			{
				var factory = ResolveSingleton(factoryMethod.DeclaringType, service.Context);
				var dependency = factory.AsSingleInstanceDependency(null);
				service.AddDependency(dependency, false);
				if (dependency.Status == ServiceStatus.Ok)
					InvokeConstructor(factoryMethod, dependency.Value, new object[0], service);
			}
			if (implementationConfiguration != null && implementationConfiguration.InstanceFilter != null)
			{
				var filteredOutCount = service.FilterInstances(implementationConfiguration.InstanceFilter);
				if (filteredOutCount > 0)
					service.SetComment("instance filter");
			}
		}

		private static MethodInfo GetFactoryOrNull(Type type)
		{
			var factoryType = type.GetNestedType("Factory");
			return factoryType == null ? null : factoryType.GetMethod("Create", Type.EmptyTypes);
		}

		public IEnumerable<Type> GetDependencies(Type type)
		{
			EnsureNotDisposed();
			if (typeof (Delegate).IsAssignableFrom(type))
				return Enumerable.Empty<Type>();
			if (!type.IsAbstract)
			{
				var result = dependenciesInjector.GetDependencies(type)
					.Select(ReflectionHelpers.UnwrapEnumerable)
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
				.Select(ReflectionHelpers.UnwrapEnumerable)
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
				service.SetError(implementation.publicConstructors.Length == 0
					? "no public ctors"
					: "many public ctors");
				return;
			}
			implementation.SetService(service);
			var formalParameters = constructor.GetParameters();
			var actualArguments = new object[formalParameters.Length];
			for (var i = 0; i < formalParameters.Length; i++)
			{
				var formalParameter = formalParameters[i];
				var dependency = InstantiateDependency(formalParameter, implementation, service.Context)
					.CastTo(formalParameter.ParameterType);
				service.AddDependency(dependency, false);
				if (dependency.ContainerService != null)
					service.UnionUsedContracts(dependency.ContainerService);
				if (service.status != ServiceStatus.Ok)
					return;
				actualArguments[i] = dependency.Value;
			}
			service.EndResolveDependencies();
			var unusedDependencyConfigurations = implementation.GetUnusedDependencyConfigurationNames().ToArray();
			if (unusedDependencyConfigurations.Length > 0)
			{
				service.SetError(string.Format("unused dependency configurations [{0}]",
					unusedDependencyConfigurations.JoinStrings(",")));
				return;
			}
			if (service.Context.DeclaredContractsCount() == service.FinalUsedContracts.Length)
			{
				InvokeConstructor(constructor, null, actualArguments, service);
				return;
			}
			var usedContactsCacheKey = new ServiceName(service.Type, service.FinalUsedContracts);
			var serviceForUsedContracts = instanceCache.GetOrAdd(usedContactsCacheKey, createContainerServiceDelegate);
			if (serviceForUsedContracts.AcquireInstantiateLock())
				try
				{
					serviceForUsedContracts.AttachToContext(service.Context);
					serviceForUsedContracts.UnionUsedContracts(service);
					serviceForUsedContracts.UnionDependencies(service);
					InvokeConstructor(constructor, null, actualArguments, serviceForUsedContracts);
					serviceForUsedContracts.EndInstantiate();
				}
				finally
				{
					serviceForUsedContracts.ReleaseInstantiateLock();
				}
			service.UnionStatusFrom(serviceForUsedContracts);
			service.UnionInstances(serviceForUsedContracts);
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
					return ServiceDependency.Error(null, formalParameter,
						"can't find resource [{0}] in namespace of [{1}], assembly [{2}]",
						resourceAttribute.Name, implementation.type, implementation.type.Assembly.GetName().Name);
				return ServiceDependency.Constant(formalParameter, resourceStream);
			}
			var dependencyType = implementationType.UnwrapEnumerable();
			var isEnumerable = dependencyType != implementationType;
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
			if (dependencyType.IsSimpleType())
			{
				if (!formalParameter.HasDefaultValue)
					return ServiceDependency.Error(null, formalParameter,
						"parameter [{0}] of service [{1}] is not configured",
						formalParameter.Name, implementation.type.FormatName());
				return ServiceDependency.Constant(formalParameter, formalParameter.DefaultValue);
			}
			var resultService = context.Resolve(dependencyType, contracts, this);
			if (resultService.status.IsBad())
				return ServiceDependency.ServiceError(resultService);
			if (isEnumerable)
				return ServiceDependency.Service(resultService, resultService.GetAllValues());
			if (resultService.status == ServiceStatus.NotResolved)
			{
				if (formalParameter.HasDefaultValue)
					return ServiceDependency.Service(resultService, formalParameter.DefaultValue);
				if (formalParameter.IsDefined<OptionalAttribute>() || formalParameter.IsDefined("CanBeNullAttribute"))
					return ServiceDependency.Service(resultService, null);
				return ServiceDependency.NotResolved(resultService);
			}
			return resultService.AsSingleInstanceDependency(null);
		}

		private static void InvokeConstructor(MethodBase method, object self, object[] actualArguments,
			ContainerService service)
		{
			try
			{
				var instance = MethodInvoker.Invoke(method, self, actualArguments);
				service.AddInstance(instance, true);
			}
			catch (ServiceCouldNotBeCreatedException e)
			{
				service.SetComment(e.Message);
			}
			catch (Exception e)
			{
				service.SetError(e);
			}
		}

		public void Dispose()
		{
			if (disposed)
				return;
			disposed = true;
			var exceptions = new List<SimpleContainerException>();
			foreach (var disposable in GetInstanceCache(typeof (IDisposable)).Reverse())
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

		private static void DisposeService(NamedInstance disposable)
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
				var message = string.Format("error disposing [{0}]", disposable.Name);
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