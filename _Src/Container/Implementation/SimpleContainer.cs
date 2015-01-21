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
			new FactoryWithArgumentsPlugin()
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

		public ResolvedService Resolve(Type serviceType, IEnumerable<string> contracts)
		{
			EnsureNotDisposed();
			Type enumerableItem;
			var isEnumerable = TryUnwrapEnumerable(serviceType, out enumerableItem);
			var typeToResolve = isEnumerable ? enumerableItem : serviceType;
			var cacheKey = new CacheKey(typeToResolve, InternalHelpers.ToInternalContracts(contracts, typeToResolve));
			var result = instanceCache.GetOrAdd(cacheKey, createInstanceDelegate);
			result.WaitForResolveOrDie();
			return new ResolvedService(result, this, isEnumerable);
		}

		internal ContainerService Create(Type type, IEnumerable<string> contracts, object arguments, ResolutionContext context)
		{
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

		public ResolvedService Create(Type type, IEnumerable<string> contracts, object arguments)
		{
			EnsureNotDisposed();
			var result = Create(type, contracts, arguments, null);
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
			return dependenciesInjector.BuildUp(target, contracts);
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
			var cacheKey = new CacheKey(type, context.RequiredContractNames());
			var result = instanceCache.GetOrAdd(cacheKey, createContainerServiceDelegate);
			if (result.AcquireInstantiateLock())
				try
				{
					context.Instantiate(name, result, this);
					result.InstantiatedSuccessfully(Interlocked.Increment(ref topSortIndex));
				}
				catch (Exception e)
				{
					result.InstantiatedUnsuccessfully(e);
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
				service.EndResolveDependencies();
				return;
			}
			if (service.Type.IsGenericType && service.Type.ContainsGenericParameters)
			{
				service.Context.Comment("has open generic arguments");
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
			if (service.Type.HasAttribute("IgnoredImplementationAttribute"))
			{
				service.Context.Comment("IgnoredImplementation");
				service.EndResolveDependencies();
				return;
			}
			var implementationConfiguration = service.Context.GetConfiguration<ImplementationConfiguration>(service.Type);
			if (implementationConfiguration != null && implementationConfiguration.DontUseIt)
			{
				service.Context.Comment("DontUse");
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
				service.Context.Comment("instance filter");
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
				service.AddDependency(dependency);
				object dependencyValue;
				if (IsEnumerable(formalParameter.ParameterType) || formalParameter.ParameterType.IsArray)
					dependencyValue = dependency.AsEnumerable();
				else if (dependency.Instances.Count == 0)
				{
					service.EndResolveDependencies();
					return;
				}
				else
					dependencyValue = dependency.SingleInstance(false);
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
			if (service.AllContractsUsed())
			{
				InvokeConstructor(constructor, null, actualArguments, service);
				return;
			}
			var usedContactsCacheKey = new CacheKey(type, service.FinalUsedContracts);
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
				catch (Exception e)
				{
					serviceForUsedContracts.InstantiatedUnsuccessfully(e);
					throw;
				}
				finally
				{
					serviceForUsedContracts.ReleaseInstantiateLock();
				}
			else
				service.Context.Comment("reused");
			foreach (var instance in serviceForUsedContracts.Instances)
				service.AddInstance(instance);
		}

		private ContainerService IndependentService(ParameterInfo formalParameter, object instance, ResolutionContext context)
		{
			var result = new ContainerService(formalParameter.ParameterType);
			result.AddInstance(instance);
			if (ReflectionHelpers.simpleTypes.Contains(formalParameter.ParameterType))
				context.LogSimpleType(formalParameter.Name, result, this);
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
				return IndependentService(formalParameter, actualArgument, service.Context);
			var parameters = implementation.GetParameters();
			if (parameters != null && parameters.TryGet(formalParameter.Name, formalParameter.ParameterType, out actualArgument))
				return IndependentService(formalParameter, actualArgument, service.Context);
			var dependencyConfiguration = implementation.GetDependencyConfiguration(formalParameter);
			Type implementationType = null;
			if (dependencyConfiguration != null)
			{
				if (dependencyConfiguration.ValueAssigned)
					return IndependentService(formalParameter, dependencyConfiguration.Value, service.Context);
				if (dependencyConfiguration.Factory != null)
					return IndependentService(formalParameter, dependencyConfiguration.Factory(this), service.Context);
				implementationType = dependencyConfiguration.ImplementationType;
			}
			implementationType = implementationType ?? formalParameter.ParameterType;
			if (ReflectionHelpers.simpleTypes.Contains(implementationType) && formalParameter.HasDefaultValue)
				return IndependentService(formalParameter, formalParameter.DefaultValue, service.Context);
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
				return IndependentService(formalParameter, resourceStream, service.Context);
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
				return IndependentService(formalParameter, instance, service.Context);
			}
			var result = service.Context.Resolve(dependencyType, formalParameter.Name, this, contracts);
			if (isEnumerable)
				return result;
			if (result.Instances.Count == 0)
			{
				if (formalParameter.HasDefaultValue)
					return IndependentService(formalParameter, formalParameter.DefaultValue, service.Context);
				if (IsOptional(formalParameter))
					return IndependentService(formalParameter, null, service.Context);
			}
			return result;
		}

		private static bool IsOptional(ICustomAttributeProvider attributes)
		{
			return attributes.IsDefined<OptionalAttribute>() || attributes.HasAttribute("CanBeNullAttribute");
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