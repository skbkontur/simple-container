using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal class SimpleContainer : IContainer
	{
		private readonly Func<ServiceName, ContainerServiceId> createId = _ => new ContainerServiceId();

		private readonly ConcurrentDictionary<ServiceName, ContainerServiceId> instanceCache =
			new ConcurrentDictionary<ServiceName, ContainerServiceId>();

		private readonly ConcurrentDictionary<ServiceName, Func<object>> factoryCache =
			new ConcurrentDictionary<ServiceName, Func<object>>();

		private readonly DependenciesInjector dependenciesInjector;
		private bool disposed;
		private readonly List<ImplementationSelector> implementationSelectors;
		internal ConfigurationRegistry Configuration { get; private set; }
		internal readonly ContainerContext containerContext;
		private readonly LogError errorLogger;

		public SimpleContainer(ConfigurationRegistry configurationRegistry, ContainerContext containerContext,
			LogError errorLogger)
		{
			Configuration = configurationRegistry;
			implementationSelectors = configurationRegistry.GetImplementationSelectors();
			dependenciesInjector = new DependenciesInjector(this);
			this.containerContext = containerContext;
			this.errorLogger = errorLogger;
		}

		public ResolvedService Resolve(Type type, IEnumerable<string> contracts)
		{
			EnsureNotDisposed();
			if (type == null)
				throw new ArgumentNullException("type");
			var t = type.UnwrapEnumerable();
			return Resolve(t, contracts, t != type);
		}

		//todo remove this ugly hack
		internal ResolvedService Resolve(Type type, IEnumerable<string> contracts, bool isEnumerable)
		{
			EnsureNotDisposed();
			if (type == null)
				throw new ArgumentNullException("type");
			var name = CreateServiceName(type, contracts);
			ContainerService result;
			if (ContainerService.ThreadInitializing)
			{
				const string formatMessage = "attempt to resolve [{0}] is prohibited to prevent possible deadlocks";
				var message = string.Format(formatMessage, name.Type.FormatName());
				result = ContainerService.Error(name, message);
			}
			else
			{
				var id = instanceCache.GetOrAdd(name, createId);
				if (!id.TryGet(out result))
				{
					var activation = ResolutionContext.Push(this);
					result = ResolveCore(name, false, null, activation.activated);
					PopResolutionContext(activation, result, isEnumerable);
				}
			}
			return new ResolvedService(result, containerContext, isEnumerable);
		}

		private void PopResolutionContext(ResolutionContext.ResolutionContextActivation activation,
			ContainerService containerService, bool isEnumerable)
		{
			ResolutionContext.Pop(activation);
			if (activation.previous == null)
				return;
			var resultDependency = containerService.AsDependency(containerContext, "() => " + containerService.Type.FormatName(),
				isEnumerable);
			if (activation.activated.Container != activation.previous.Container)
				resultDependency.Comment = "container boundary";
			activation.previous.TopBuilder.AddDependency(resultDependency, false);
		}

		public object Create(Type type, IEnumerable<string> contracts, object arguments)
		{
			EnsureNotDisposed();
			if (type == null)
				throw new ArgumentNullException("type");
			var name = CreateServiceName(type.UnwrapEnumerable(), contracts);
			return Create(name, name.Type != type, arguments);
		}

		internal object Create(ServiceName name, bool isEnumerable, object arguments)
		{
			Func<object> compiledFactory;
			var hasPendingResolutionContext = ResolutionContext.HasPendingResolutionContext;
			if (arguments == null && factoryCache.TryGetValue(name, out compiledFactory) && !hasPendingResolutionContext)
				return compiledFactory();
			var activation = ResolutionContext.Push(this);
			List<string> oldContracts = null;
			if (hasPendingResolutionContext)
			{
				oldContracts = activation.activated.Contracts.Replace(name.Contracts);
				name = new ServiceName(name.Type);
			}
			var result = ResolveCore(name, true, ObjectAccessor.Get(arguments), activation.activated);
			if (hasPendingResolutionContext)
				activation.activated.Contracts.Restore(oldContracts);
			PopResolutionContext(activation, result, isEnumerable);
			if (!hasPendingResolutionContext)
				result.EnsureInitialized(containerContext, result);
			result.CheckStatusIsGood(containerContext);
			if (isEnumerable)
				return result.GetAllValues();
			result.CheckSingleValue(containerContext);
			return result.Instances[0].Instance;
		}

		private ServiceConfiguration GetConfigurationWithoutContracts(Type type)
		{
			return GetConfigurationOrNull(type, new ContractsList());
		}

		internal ServiceConfiguration GetConfiguration(Type type, ResolutionContext context)
		{
			return GetConfigurationOrNull(type, context.Contracts) ?? ServiceConfiguration.empty;
		}

		private ServiceConfiguration GetConfigurationOrNull(Type type, ContractsList contracts)
		{
			var result = Configuration.GetConfigurationOrNull(type, contracts);
			if (result == null && type.IsGenericType)
				result = Configuration.GetConfigurationOrNull(type.GetDefinition(), contracts);
			return result;
		}

		public IEnumerable<Type> GetImplementationsOf(Type interfaceType)
		{
			EnsureNotDisposed();
			if (interfaceType == null)
				throw new ArgumentNullException("interfaceType");
			var interfaceConfiguration = GetConfigurationWithoutContracts(interfaceType);
			if (interfaceConfiguration != null && interfaceConfiguration.ImplementationTypes != null)
				return interfaceConfiguration.ImplementationTypes;
			return containerContext.typesList.InheritorsOf(interfaceType)
				.Where(delegate(Type type)
				{
					var implementationConfiguration = GetConfigurationWithoutContracts(type);
					return implementationConfiguration == null || !implementationConfiguration.DontUseIt;
				})
				.ToArray();
		}

		public BuiltUpService BuildUp(object target, IEnumerable<string> contracts)
		{
			EnsureNotDisposed();
			if (target == null)
				throw new ArgumentNullException("target");
			return dependenciesInjector.BuildUp(CreateServiceName(target.GetType(), contracts), target);
		}

		private static ServiceName CreateServiceName(Type type, IEnumerable<string> contracts)
		{
			var contractsArray = contracts == null
				? InternalHelpers.emptyStrings
				: (contracts is string[] ? (string[]) contracts : contracts.ToArray());
			for (var i = 0; i < contractsArray.Length; i++)
			{
				var contract = contractsArray[i];
				if (string.IsNullOrEmpty(contract))
				{
					var message = string.Format("invalid contracts [{0}] - empty ones found", contractsArray.JoinStrings(","));
					throw new SimpleContainerException(message);
				}
				for (var j = 0; j < i; j++)
					if (contractsArray[j].EqualsIgnoringCase(contract))
					{
						var message = string.Format("invalid contracts [{0}] - duplicates found", contractsArray.JoinStrings(","));
						throw new SimpleContainerException(message);
					}
			}
			return ServiceName.Parse(type, contractsArray);
		}

		private IEnumerable<ServiceInstance> GetInstanceCache(Type interfaceType)
		{
			var seen = new HashSet<object>();
			var target = new List<ServiceInstance>();
			foreach (var wrap in instanceCache.Values)
			{
				ContainerService service;
				if (wrap.TryGet(out service))
					service.CollectInstances(interfaceType, seen, target);
			}
			return target;
		}

		public IContainer Clone(Action<ContainerConfigurationBuilder> configure)
		{
			EnsureNotDisposed();
			return new SimpleContainer(Configuration.Apply(containerContext.typesList, configure), containerContext, null);
		}

		public Type[] AllTypes
		{
			get { return containerContext.AllTypes(); }
		}

		internal ContainerService ResolveCore(ServiceName name, bool createNew,
			IObjectAccessor arguments, ResolutionContext context)
		{
			if (context.HasCycle(name))
			{
				var message = string.Format("cyclic dependency for service [{0}], stack\r\n{1}",
					name.Type.FormatName(), context.FormatStack() + "\r\n\t" + name);
				return ContainerService.Error(name, message);
			}
			context.ConstructingServices.Add(name);
			var pushedContracts = context.Contracts.Push(name.Contracts);
			if (!pushedContracts.isOk)
			{
				const string messageFormat = "contract [{0}] already declared, stack\r\n{1}";
				var message = string.Format(messageFormat, pushedContracts.duplicatedContractName,
					context.FormatStack() + "\r\n\t" + name);
				context.Contracts.RemoveLast(pushedContracts.pushedContractsCount);
				context.ConstructingServices.Remove(name);
				return ContainerService.Error(name, message);
			}
			ServiceConfiguration configuration = null;
			Exception configurationException = null;
			try
			{
				configuration = GetConfiguration(name.Type, context);
			}
			catch (Exception e)
			{
				configurationException = e;
			}
			var declaredName = new ServiceName(name.Type, context.Contracts.Snapshot());
			if (configuration != null && configuration.FactoryDependsOnTarget && context.Stack.Count > 0)
				declaredName = declaredName.AddContracts(context.TopBuilder.Type.FormatName());
			ContainerServiceId id = null;
			if (!createNew)
			{
				id = instanceCache.GetOrAdd(declaredName, createId);
				var acquireResult = id.AcquireInstantiateLock();
				if (!acquireResult.acquired)
				{
					context.ConstructingServices.Remove(name);
					context.Contracts.RemoveLast(pushedContracts.pushedContractsCount);
					return acquireResult.alreadyConstructedService;
				}
			}
			var builder = new ContainerService.Builder(name);
			context.Stack.Add(builder);
			builder.Context = context;
			builder.DeclaredContracts = declaredName.Contracts;
			if (configuration == null)
				builder.SetError(configurationException);
			else
			{
				builder.SetConfiguration(configuration);
				var expandedUnions = context.Contracts.TryExpandUnions(Configuration);
				if (expandedUnions != null)
				{
					var poppedContracts = context.Contracts.PopMany(expandedUnions.Length);
					foreach (var c in expandedUnions.CartesianProduct())
					{
						var childService = ResolveCore(new ServiceName(name.Type, c), createNew, arguments, context);
						builder.LinkTo(containerContext, childService, null);
						if (builder.Status.IsBad())
							break;
					}
					context.Contracts.PushNoCheck(poppedContracts);
				}
				else
				{
					builder.CreateNew = createNew;
					builder.Arguments = arguments;
					Instantiate(builder);
				}
			}
			context.ConstructingServices.Remove(name);
			context.Contracts.RemoveLast(pushedContracts.pushedContractsCount);
			context.Stack.RemoveLast();
			var result = builder.GetService();
			if (id != null)
				id.ReleaseInstantiateLock(builder.Context.AnalizeDependenciesOnly ? null : result);
			return result;
		}

		internal void Instantiate(ContainerService.Builder builder)
		{
			LifestyleAttribute lifestyle;
			if (builder.Type.IsSimpleType())
				builder.SetError("can't create simple type");
			else if (builder.Type == typeof (IContainer))
				builder.AddInstance(this, false, false);
			else if (builder.Configuration.ImplementationAssigned)
				builder.AddInstance(builder.Configuration.Implementation, builder.Configuration.ContainerOwnsInstance, true);
			else if (builder.Configuration.Factory != null)
			{
				if (!builder.Context.AnalizeDependenciesOnly)
					builder.CreateInstanceBy(CallTarget.F(builder.Configuration.Factory), builder.Configuration.ContainerOwnsInstance);
			}
			else if (builder.Type.IsValueType)
				builder.SetError("can't create value type");
			else if (builder.Type.IsGenericType && builder.Type.ContainsGenericParameters)
				builder.SetError("can't create open generic");
			else if (!builder.CreateNew && builder.Type.TryGetCustomAttribute(out lifestyle) &&
			         lifestyle.Lifestyle == Lifestyle.PerRequest)
			{
				const string messageFormat = "service [{0}] with PerRequest lifestyle can't be resolved, use Func<{0}> instead";
				builder.SetError(string.Format(messageFormat, builder.Type.FormatName()));
			}
			else if (builder.Type.IsAbstract)
				InstantiateInterface(builder);
			else
				InstantiateImplementation(builder);
		}

		private void ApplySelectors(HashSet<ImplementationType> implementations, ContainerService.Builder builder)
		{
			if (builder.Configuration != ServiceConfiguration.empty)
				return;
			if (implementationSelectors.Count == 0)
				return;
			var selectorDecisions = new List<ImplementationSelectorDecision>();
			var typesArray = implementations.Select(x => x.type).ToArray();
			foreach (var s in implementationSelectors)
				s(builder.Type, typesArray, selectorDecisions);
			foreach (var decision in selectorDecisions)
			{
				var item = new ImplementationType
				{
					type = decision.target,
					comment = decision.comment,
					accepted = decision.action == ImplementationSelectorDecision.Action.Include
				};
				implementations.Replace(item);
			}
		}

		private void InstantiateInterface(ContainerService.Builder builder)
		{
			var implementationTypes = GetImplementationTypes(builder);
			ApplySelectors(implementationTypes, builder);
			if (implementationTypes.Count == 0)
			{
				builder.SetComment("has no implementations");
				return;
			}
			Func<object> factory = null;
			var canUseFactory = true;
			foreach (var implementationType in implementationTypes)
				if (implementationType.accepted)
				{
					var implementationService = ResolveCore(ServiceName.Parse(implementationType.type, InternalHelpers.emptyStrings),
						builder.CreateNew, builder.Arguments, builder.Context);
					builder.LinkTo(containerContext, implementationService, implementationType.comment);
					if (builder.CreateNew && builder.Arguments == null &&
					    implementationService.Status == ServiceStatus.Ok && canUseFactory)
						if (factory == null)
						{
							if (!factoryCache.TryGetValue(implementationService.Name, out factory))
								canUseFactory = false;
						}
						else
							canUseFactory = false;
					if (builder.Status.IsBad())
						return;
				}
				else
				{
					var dependency = containerContext.NotResolved(null, implementationType.type.FormatName());
					dependency.Comment = implementationType.comment;
					builder.AddDependency(dependency, true);
				}
			builder.EndResolveDependencies();
			if (factory != null && canUseFactory)
				factoryCache.TryAdd(builder.GetFinalName(), factory);
		}

		private HashSet<ImplementationType> GetImplementationTypes(ContainerService.Builder builder)
		{
			var result = new HashSet<ImplementationType>();
			var candidates = builder.Configuration.ImplementationTypes ??
			                 containerContext.typesList.InheritorsOf(builder.Type.GetDefinition());
			var implementationTypesAreExplicitlyConfigured = builder.Configuration.ImplementationTypes != null;
			foreach (var implType in candidates)
			{
				if (!implementationTypesAreExplicitlyConfigured)
				{
					var configuration = GetConfiguration(implType, builder.Context);
					if (configuration.IgnoredImplementation || implType.IsDefined("IgnoredImplementationAttribute"))
						result.Add(new ImplementationType
						{
							type = implType,
							comment = "IgnoredImplementation",
							accepted = false
						});
				}
				if (!implType.IsGenericType)
				{
					if (!builder.Type.IsGenericType || builder.Type.IsAssignableFrom(implType))
						result.Add(ImplementationType.Accepted(implType));
				}
				else if (!implType.ContainsGenericParameters)
					result.Add(ImplementationType.Accepted(implType));
				else
				{
					var mapped = containerContext.genericsAutoCloser.AutoCloseDefinition(implType);
					foreach (var type in mapped)
						if (builder.Type.IsAssignableFrom(type))
							result.Add(ImplementationType.Accepted(type));
					if (builder.Type.IsGenericType)
					{
						var implInterfaces = implType.ImplementationsOf(builder.Type.GetGenericTypeDefinition());
						foreach (var implInterface in implInterfaces)
						{
							var closed = implType.TryCloseByPattern(implInterface, builder.Type);
							if (closed != null)
								result.Add(ImplementationType.Accepted(closed));
						}
					}
					if (builder.Arguments == null)
						continue;
					var serviceConstructor = implType.GetConstructor();
					if (!serviceConstructor.isOk)
						continue;
					foreach (var formalParameter in serviceConstructor.value.GetParameters())
					{
						if (!formalParameter.ParameterType.ContainsGenericParameters)
							continue;
						ValueWithType parameterValue;
						if (!builder.Arguments.TryGet(formalParameter.Name, out parameterValue))
							continue;
						var parameterType = parameterValue.value == null ? parameterValue.type : parameterValue.value.GetType();
						var implInterfaces = formalParameter.ParameterType.IsGenericParameter
							? new List<Type>(1) {parameterType}
							: parameterType.ImplementationsOf(formalParameter.ParameterType.GetGenericTypeDefinition());
						foreach (var implInterface in implInterfaces)
						{
							var closedItem = implType.TryCloseByPattern(formalParameter.ParameterType, implInterface);
							if (closedItem != null)
								result.Add(ImplementationType.Accepted(closedItem));
						}
					}
				}
			}
			return result;
		}

		public IEnumerable<Type> GetDependencies(Type type)
		{
			EnsureNotDisposed();
			if (type.IsDelegate())
				return Enumerable.Empty<Type>();
			if (!type.IsAbstract)
			{
				var result = dependenciesInjector.GetDependencies(type)
					.Select(ReflectionHelpers.UnwrapEnumerable)
					.ToArray();
				if (result.Any())
					return result;
			}
			var serviceConstructor = type.GetConstructor();
			if (!serviceConstructor.isOk)
				return Enumerable.Empty<Type>();
			var typeConfiguration = GetConfigurationWithoutContracts(type);
			return serviceConstructor.value.GetParameters()
				.Where(p => typeConfiguration == null || typeConfiguration.GetOrNull(p) == null)
				.Select(x => x.ParameterType)
				.Select(ReflectionHelpers.UnwrapEnumerable)
				.Where(p => GetConfigurationWithoutContracts(p) == null)
				.Where(IsDependency);
		}

		private static bool IsDependency(Type type)
		{
			if (type.IsDelegate())
				return false;
			if (type.IsSimpleType())
				return false;
			if (type.IsArray && type.GetElementType().IsSimpleType())
				return false;
			return true;
		}

		private void InstantiateImplementation(ContainerService.Builder builder)
		{
			if (builder.DontUse())
			{
				builder.SetComment("DontUse");
				return;
			}
			var result = FactoryCreator.TryCreate(builder) ?? LazyCreator.TryCreate(builder);
			if (result != null)
			{
				builder.AddInstance(result, true, false);
				return;
			}
			if (NestedFactoryCreator.TryCreate(builder))
				return;
			if (CtorFactoryCreator.TryCreate(builder))
				return;
			if (builder.Type.IsDelegate())
			{
				builder.SetError(string.Format("can't create delegate [{0}]", builder.Type.FormatName()));
				return;
			}
			var constructor = builder.Type.GetConstructor();
			if (!constructor.isOk)
			{
				builder.SetError(constructor.errorMessage);
				return;
			}
			var formalParameters = constructor.value.GetParameters();
			var actualArguments = new object[formalParameters.Length];
			var hasServiceNameParameters = false;
			for (var i = 0; i < formalParameters.Length; i++)
			{
				var formalParameter = formalParameters[i];
				if (formalParameter.ParameterType == typeof (ServiceName))
				{
					hasServiceNameParameters = true;
					continue;
				}
				var dependency = InstantiateDependency(formalParameter, builder).CastTo(formalParameter.ParameterType);
				builder.AddDependency(dependency, false);
				if (dependency.ContainerService != null)
					builder.UnionUsedContracts(dependency.ContainerService);
				if (builder.Status != ServiceStatus.Ok && !builder.Context.AnalizeDependenciesOnly)
					return;
				actualArguments[i] = dependency.Value;
			}
			foreach (var d in builder.Configuration.ImplicitDependencies)
			{
				var dependency = ResolveCore(d, false, null, builder.Context).AsDependency(containerContext, null, false);
				dependency.Comment = "implicit";
				builder.AddDependency(dependency, false);
				if (dependency.ContainerService != null)
					builder.UnionUsedContracts(dependency.ContainerService);
				if (builder.Status != ServiceStatus.Ok)
					return;
			}
			builder.EndResolveDependencies();
			if (builder.Context.AnalizeDependenciesOnly)
				return;
			var dependenciesResolvedByArguments = builder.Arguments == null ? null : builder.Arguments.GetUsed();
			List<string> unusedConfigurationKeys = null;
			foreach (var k in builder.Configuration.GetUnusedDependencyKeys())
			{
				var resolvedByArguments = dependenciesResolvedByArguments != null &&
				                          k.name != null &&
				                          dependenciesResolvedByArguments.Contains(k.name);
				if (resolvedByArguments)
					continue;
				if (unusedConfigurationKeys == null)
					unusedConfigurationKeys = new List<string>();
				unusedConfigurationKeys.Add(k.ToString());
			}
			if (unusedConfigurationKeys != null)
			{
				builder.SetError(string.Format("unused dependency configurations [{0}]", unusedConfigurationKeys.JoinStrings(",")));
				return;
			}
			if (hasServiceNameParameters)
				for (var i = 0; i < formalParameters.Length; i++)
					if (formalParameters[i].ParameterType == typeof (ServiceName))
						actualArguments[i] = builder.GetFinalName();
			if (builder.CreateNew || builder.DeclaredContracts.Length == builder.FinalUsedContracts.Length)
			{
				builder.CreateInstanceBy(CallTarget.M(constructor.value, null, actualArguments), true);
				if (builder.CreateNew && builder.Arguments == null)
				{
					var compiledConstructor = constructor.value.Compile();
					factoryCache.TryAdd(builder.GetFinalName(), () =>
					{
						var instance = compiledConstructor(null, actualArguments);
						var component = instance as IInitializable;
						if (component != null)
							component.Initialize();
						return instance;
					});
				}
				return;
			}
			var serviceForUsedContractsId = instanceCache.GetOrAdd(builder.GetFinalName(), createId);
			var acquireResult = serviceForUsedContractsId.AcquireInstantiateLock();
			if (acquireResult.acquired)
			{
				builder.CreateInstanceBy(CallTarget.M(constructor.value, null, actualArguments), true);
				serviceForUsedContractsId.ReleaseInstantiateLock(builder.Context.AnalizeDependenciesOnly
					? null
					: builder.GetService());
			}
			else
				builder.Reuse(acquireResult.alreadyConstructedService);
		}

		internal ServiceDependency InstantiateDependency(ParameterInfo formalParameter, ContainerService.Builder builder)
		{
			ValueWithType actualArgument;
			if (builder.Arguments != null && builder.Arguments.TryGet(formalParameter.Name, out actualArgument))
				return containerContext.Constant(formalParameter, actualArgument.value);
			var parameters = builder.Configuration.ParametersSource;
			object actualParameter;
			if (parameters != null && parameters.TryGet(formalParameter.Name, formalParameter.ParameterType, out actualParameter))
				return containerContext.Constant(formalParameter, actualParameter);
			var dependencyConfiguration = builder.Configuration.GetOrNull(formalParameter);
			Type implementationType = null;
			if (dependencyConfiguration != null)
			{
				if (dependencyConfiguration.ValueAssigned)
					return containerContext.Constant(formalParameter, dependencyConfiguration.Value);
				if (dependencyConfiguration.Factory != null)
				{
					var dependencyBuilder = new ContainerService.Builder(new ServiceName(formalParameter.ParameterType))
					{
						Context = builder.Context,
						DependencyName = formalParameter.Name
					};
					builder.Context.Stack.Add(dependencyBuilder);
					dependencyBuilder.CreateInstanceBy(CallTarget.F(dependencyConfiguration.Factory), true);
					builder.Context.Stack.RemoveLast();
					return dependencyBuilder.GetService().AsDependency(containerContext, formalParameter.Name, false);
				}
				implementationType = dependencyConfiguration.ImplementationType;
			}
			implementationType = implementationType ?? formalParameter.ParameterType;
			FromResourceAttribute resourceAttribute;
			if (implementationType == typeof (Stream) && formalParameter.TryGetCustomAttribute(out resourceAttribute))
			{
				var resourceStream = builder.Type.Assembly.GetManifestResourceStream(builder.Type, resourceAttribute.Name);
				if (resourceStream == null)
					return containerContext.Error(null, formalParameter.Name,
						"can't find resource [{0}] in namespace of [{1}], assembly [{2}]",
						resourceAttribute.Name, builder.Type, builder.Type.Assembly.GetName().Name);
				return containerContext.Resource(formalParameter, resourceAttribute.Name, resourceStream);
			}
			var dependencyName = ServiceName.Parse(implementationType.UnwrapEnumerable(),
				InternalHelpers.ParseContracts(formalParameter));
			if (dependencyName.Type.IsSimpleType())
			{
				if (!formalParameter.HasDefaultValue)
					return containerContext.Error(null, formalParameter.Name,
						"parameter [{0}] of service [{1}] is not configured",
						formalParameter.Name, builder.Type.FormatName());
				return containerContext.Constant(formalParameter, formalParameter.DefaultValue);
			}
			var resultService = ResolveCore(dependencyName, false, null, builder.Context);
			if (resultService.Status.IsBad())
				return containerContext.ServiceError(resultService);
			var isEnumerable = dependencyName.Type != implementationType;
			if (isEnumerable)
				return containerContext.Service(resultService, resultService.GetAllValues());
			if (resultService.Status == ServiceStatus.NotResolved)
			{
				if (formalParameter.HasDefaultValue)
					return containerContext.Service(resultService, formalParameter.DefaultValue);
				if (formalParameter.IsDefined<OptionalAttribute>() || formalParameter.IsDefined("CanBeNullAttribute"))
					return containerContext.Service(resultService, null);
				return containerContext.NotResolved(resultService);
			}
			return resultService.AsDependency(containerContext, null, false);
		}

		public void Dispose()
		{
			if (disposed)
				return;
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
			disposed = true;
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
				disposable.ContainerService.disposing = true;
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
				var instanceName = new ServiceName(disposable.Instance.GetType(), disposable.ContainerService.UsedContracts);
				var message = string.Format("error disposing [{0}]", instanceName);
				throw new SimpleContainerException(message, e);
			}
			finally
			{
				disposable.ContainerService.disposing = false;
			}
		}

		private void EnsureNotDisposed()
		{
			if (disposed)
				throw new ObjectDisposedException("SimpleContainer");
		}
	}
}