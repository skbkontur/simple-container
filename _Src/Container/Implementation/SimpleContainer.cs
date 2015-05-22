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
		private readonly Func<ServiceName, ContainerServiceId> createWrap;

		private static readonly IFactoryPlugin[] factoryPlugins =
		{
			new SimpleFactoryPlugin(),
			new FactoryWithArgumentsPlugin(),
			new LazyFactoryPlugin()
		};

		protected readonly IConfigurationRegistry configurationRegistry;
		protected readonly IInheritanceHierarchy inheritors;
		private readonly StaticContainer staticContainer;
		protected readonly CacheLevel cacheLevel;
		protected readonly LogError errorLogger;
		protected readonly LogInfo infoLogger;
		protected readonly ISet<Type> staticServices;

		private readonly ConcurrentDictionary<ServiceName, ContainerServiceId> instanceCache =
			new ConcurrentDictionary<ServiceName, ContainerServiceId>();

		private readonly DependenciesInjector dependenciesInjector;
		private bool disposed;

		public SimpleContainer(IConfigurationRegistry configurationRegistry, IInheritanceHierarchy inheritors,
			StaticContainer staticContainer, CacheLevel cacheLevel, ISet<Type> staticServices, LogError errorLogger,
			LogInfo infoLogger)
		{
			this.configurationRegistry = new ConfigurationRegistryWithGenericDefinitionFallback(configurationRegistry);
			this.inheritors = inheritors;
			this.staticContainer = staticContainer;
			this.cacheLevel = cacheLevel;
			dependenciesInjector = new DependenciesInjector(this);
			createWrap = k => new ContainerServiceId();
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
			var wrap = instanceCache.GetOrAdd(cacheKey, createWrap);
			ContainerService result;
			if (!wrap.TryGet(out result))
				result = ResolveSingleton(cacheKey.Type, new ResolutionContext(configurationRegistry, cacheKey.Contracts));
			return new ResolvedService(result, this, typeToResolve != type);
		}

		internal ContainerService.Builder NewService(Type type)
		{
			return new ContainerService.Builder(type, cacheLevel);
		}

		internal ContainerService Create(Type type, IEnumerable<string> contracts, object arguments, ResolutionContext context)
		{
			context = context ?? new ResolutionContext(configurationRegistry, InternalHelpers.ToInternalContracts(contracts, type));
			var resultBuilder = NewService(type).ForFactory(ObjectAccessor.Get(arguments), true);
			context.Instantiate(resultBuilder, this, null);
			var result = resultBuilder.Build();
			if (result.Status.IsGood() && resultBuilder.Arguments != null)
			{
				var unused = resultBuilder.Arguments.GetUnused().ToArray();
				if (unused.Any())
					resultBuilder.SetError(string.Format("arguments [{0}] are not used", unused.JoinStrings(",")));
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
			return new ResolvedService(result, this, false);
		}

		internal void Run(ContainerService containerService, string constructionLog)
		{
			if (constructionLog != null && infoLogger != null)
				infoLogger(new ServiceName(containerService.Type, containerService.FinalUsedContracts), "\r\n" + constructionLog);
			containerService.EnsureRunCalled(infoLogger);
		}

		private ServiceConfiguration GetConfigurationWithoutContracts(Type type)
		{
			return configurationRegistry.GetConfiguration(type, new List<string>());
		}

		public IEnumerable<Type> GetImplementationsOf(Type interfaceType)
		{
			EnsureNotDisposed();
			if (interfaceType == null)
				throw new ArgumentNullException("interfaceType");
			var interfaceConfiguration = GetConfigurationWithoutContracts(interfaceType);
			if (interfaceConfiguration != null && interfaceConfiguration.ImplementationTypes != null)
				return interfaceConfiguration.ImplementationTypes;
			var result = inheritors.GetOrNull(interfaceType);
			return result != null
				? result.Where(delegate(Type type)
				{
					var implementationConfiguration = GetConfigurationWithoutContracts(type);
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
			foreach (var wrap in instanceCache.Values)
			{
				ContainerService service;
				if (wrap.TryGet(out service))
					service.CollectInstances(interfaceType, cacheLevel, seen, target);
			}
			return target;
		}

		public IContainer Clone(Action<ContainerConfigurationBuilder> configure)
		{
			EnsureNotDisposed();
			return new SimpleContainer(CloneConfiguration(configure), inheritors, staticContainer,
				cacheLevel, staticServices, null, infoLogger);
		}

		protected IConfigurationRegistry CloneConfiguration(Action<ContainerConfigurationBuilder> configure)
		{
			if (configure == null)
				return configurationRegistry;
			var builder = new ContainerConfigurationBuilder(staticServices, cacheLevel == CacheLevel.Static);
			configure(builder);
			return new MergedConfiguration(configurationRegistry, builder.RegistryBuilder.Build());
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
			var id = instanceCache.GetOrAdd(cacheKey, createWrap);
			var cycle = context.BuilderByToken(id);
			if (cycle != null)
			{
				var previous = context.GetTopService();
				var message = string.Format("cyclic dependency {0} ...-> {1} -> {0}",
					type.FormatName(), previous == null ? "null" : previous.Type.FormatName());
				var cycleBuilder = NewService(type);
				cycleBuilder.SetError(message);
				return cycleBuilder.Build();
			}
			ContainerService result;
			if (!id.AcquireInstantiateLock(out result))
				return result;
			var resultBuilder = NewService(type);
			context.Instantiate(resultBuilder, this, id);
			result = resultBuilder.Build();
			id.ReleaseInstantiateLock(result);
			return result;
		}

		internal void Instantiate(ContainerService.Builder builder)
		{
			if (builder.Type.IsSimpleType())
			{
				builder.SetError("can't create simple type");
				return;
			}
			if (builder.Type == typeof (IContainer))
			{
				builder.AddInstance(this, false);
				return;
			}
			IEnumerable<Type> implementationTypes = null;
			var useAutosearch = false;
			if (builder.Configuration != null)
			{
				if (builder.Configuration.ImplementationAssigned)
				{
					builder.AddInstance(builder.Configuration.Implementation, builder.Configuration.ContainerOwnsInstance);
					return;
				}
				if (builder.Configuration.Factory != null)
				{
					builder.AddInstance(builder.Configuration.Factory(new FactoryContext
					{
						container = this,
						contracts = builder.DeclaredContracts
					}), builder.Configuration.ContainerOwnsInstance);
					return;
				}
				implementationTypes = builder.Configuration.ImplementationTypes;
				useAutosearch = builder.Configuration.UseAutosearch;
			}
			if (builder.Type.IsValueType)
			{
				builder.SetError("can't create value type");
				return;
			}
			if (factoryPlugins.Any(p => p.TryInstantiate(this, builder)))
				return;
			if (builder.Type.IsGenericType && builder.Type.ContainsGenericParameters)
			{
				builder.SetError("can't create open generic");
				return;
			}
			if (builder.Type.IsAbstract)
				InstantiateInterface(builder, implementationTypes, useAutosearch);
			else
				InstantiateImplementation(builder);
		}

		public static IEnumerable<Type> ProcessGenerics(Type interfaceType, IEnumerable<Type> implTypes)
		{
			foreach (var implType in implTypes)
			{
				if (!implType.IsGenericType)
				{
					if (!interfaceType.IsGenericType || interfaceType.IsAssignableFrom(implType))
						yield return implType;
				}
				else if (!implType.ContainsGenericParameters)
					yield return implType;
				else
					foreach (var type in implType.CloseBy(interfaceType, implType))
						yield return type;
			}
		}

		private void InstantiateInterface(ContainerService.Builder builder, IEnumerable<Type> implementationTypes, bool useAutosearch)
		{
			var localTypes = implementationTypes == null || useAutosearch
				? implementationTypes.EmptyIfNull()
					.Union(inheritors.GetOrNull(builder.Type.GetDefinition()).EmptyIfNull())
				: implementationTypes;
			var localTypesArray = ProcessGenerics(builder.Type, localTypes).ToArray();
			if (localTypesArray.Length == 0)
			{
				builder.SetComment("has no implementations");
				return;
			}
			foreach (var implementationType in localTypesArray)
			{
				ContainerService childService;
				if (builder.CreateNew)
				{
					var childServiceBuilder = NewService(implementationType).ForFactory(builder.Arguments, false);
					builder.Context.Instantiate(childServiceBuilder, this, null);
					childService = childServiceBuilder.Build();
				}
				else
					childService = builder.Context.Resolve(implementationType, null, this);
				if (!builder.LinkTo(childService))
					return;
			}
		}

		private void InstantiateImplementation(ContainerService.Builder builder)
		{
			if (builder.Type.IsDefined("IgnoredImplementationAttribute"))
			{
				builder.SetComment("IgnoredImplementation");
				return;
			}
			if (builder.Configuration != null && builder.Configuration.DontUseIt)
			{
				builder.SetComment("DontUse");
				return;
			}
			var factoryMethod = GetFactoryOrNull(builder.Type);
			if (factoryMethod == null)
				DefaultInstantiateImplementation(builder);
			else
			{
				var factory = ResolveSingleton(factoryMethod.DeclaringType, builder.Context);
				var dependency = factory.AsSingleInstanceDependency(null);
				builder.AddDependency(dependency, false);
				if (dependency.Status == ServiceStatus.Ok)
					InvokeConstructor(factoryMethod, dependency.Value, new object[0], builder);
			}
			if (builder.Configuration != null && builder.Configuration.InstanceFilter != null)
			{
				var filteredOutCount = builder.FilterInstances(builder.Configuration.InstanceFilter);
				if (filteredOutCount > 0)
					builder.SetComment("instance filter");
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
			var constructors = new ConstructorsInfo(type);
			ConstructorInfo constructor;
			if (!constructors.TryGetConstructor(out constructor))
				return Enumerable.Empty<Type>();
			var typeConfiguration = GetConfigurationWithoutContracts(type);
			return constructor.GetParameters()
				.Where(p => typeConfiguration == null || typeConfiguration.GetOrNull(p) == null)
				.Select(x => x.ParameterType)
				.Select(ReflectionHelpers.UnwrapEnumerable)
				.Where(p => GetConfigurationWithoutContracts(p) == null)
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

		private void DefaultInstantiateImplementation(ContainerService.Builder builder)
		{
			var constructors = new ConstructorsInfo(builder.Type);
			ConstructorInfo constructor;
			if (!constructors.TryGetConstructor(out constructor))
			{
				builder.SetError(constructors.publicConstructors.Length == 0 ? "no public ctors" : "many public ctors");
				return;
			}
			var formalParameters = constructor.GetParameters();
			var actualArguments = new object[formalParameters.Length];
			for (var i = 0; i < formalParameters.Length; i++)
			{
				var formalParameter = formalParameters[i];
				var dependency = InstantiateDependency(formalParameter, builder).CastTo(formalParameter.ParameterType);
				builder.AddDependency(dependency, false);
				if (dependency.ContainerService != null)
					builder.UnionUsedContracts(dependency.ContainerService);
				if (builder.Status != ServiceStatus.Ok)
					return;
				actualArguments[i] = dependency.Value;
			}
			builder.EndResolveDependencies();
			var unusedDependencyConfigurations = builder.Configuration != null
				? builder.Configuration.GetUnusedDependencyConfigurationKeys()
				: new string[0];
			if (unusedDependencyConfigurations.Length > 0)
			{
				builder.SetError(string.Format("unused dependency configurations [{0}]",
					unusedDependencyConfigurations.JoinStrings(",")));
				return;
			}
			if (builder.DeclaredContracts.Length == builder.FinalUsedContracts.Length)
			{
				InvokeConstructor(constructor, null, actualArguments, builder);
				return;
			}
			var usedContactsCacheKey = new ServiceName(builder.Type, builder.FinalUsedContracts);
			var serviceForUsedContractsId = instanceCache.GetOrAdd(usedContactsCacheKey, createWrap);
			ContainerService serviceForUsedContracts;
			if (serviceForUsedContractsId.AcquireInstantiateLock(out serviceForUsedContracts))
			{
				InvokeConstructor(constructor, null, actualArguments, builder);
				serviceForUsedContracts = builder.Build();
				serviceForUsedContractsId.ReleaseInstantiateLock(serviceForUsedContracts);
			}
			else
				builder.Borrow(serviceForUsedContracts);
		}

		private ServiceDependency InstantiateDependency(ParameterInfo formalParameter, ContainerService.Builder builder)
		{
			object actualArgument;
			if (builder.Arguments != null && builder.Arguments.TryGet(formalParameter.Name, out actualArgument))
				return ServiceDependency.Constant(formalParameter, actualArgument);
			var parameters = builder.Configuration == null ? null : builder.Configuration.ParametersSource;
			if (parameters != null && parameters.TryGet(formalParameter.Name, formalParameter.ParameterType, out actualArgument))
				return ServiceDependency.Constant(formalParameter, actualArgument);
			var dependencyConfiguration = builder.GetDependencyConfiguration(formalParameter);
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
				var resourceStream = builder.Type.Assembly.GetManifestResourceStream(builder.Type, resourceAttribute.Name);
				if (resourceStream == null)
					return ServiceDependency.Error(null, formalParameter,
						"can't find resource [{0}] in namespace of [{1}], assembly [{2}]",
						resourceAttribute.Name, builder.Type, builder.Type.Assembly.GetName().Name);
				return ServiceDependency.Constant(formalParameter, resourceStream);
			}
			var dependencyType = implementationType.UnwrapEnumerable();
			var isEnumerable = dependencyType != implementationType;
			var attribute = formalParameter.GetCustomAttributeOrNull<RequireContractAttribute>();
			var contracts = attribute == null ? null : new List<string>(1) {attribute.ContractName};
			var interfaceConfiguration = builder.Context.GetConfiguration(dependencyType);
			if (interfaceConfiguration != null && interfaceConfiguration.Factory != null)
			{
				var declaredContracts = new List<string>(builder.Context.DeclaredContractNames());
				if (contracts != null)
					declaredContracts.AddRange(contracts);
				var instance = interfaceConfiguration.Factory(new FactoryContext
				{
					container = this,
					target = builder.Type,
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
						formalParameter.Name, builder.Type.FormatName());
				return ServiceDependency.Constant(formalParameter, formalParameter.DefaultValue);
			}
			var resultService = builder.Context.Resolve(dependencyType, contracts, this);
			if (resultService.Status.IsBad())
				return ServiceDependency.ServiceError(resultService);
			if (isEnumerable)
				return ServiceDependency.Service(resultService, resultService.GetAllValues());
			if (resultService.Status == ServiceStatus.NotResolved)
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
			ContainerService.Builder builder)
		{
			try
			{
				var instance = MethodInvoker.Invoke(method, self, actualArguments);
				builder.AddInstance(instance, true);
			}
			catch (ServiceCouldNotBeCreatedException e)
			{
				builder.SetComment(e.Message);
			}
			catch (Exception e)
			{
				builder.SetError(e);
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