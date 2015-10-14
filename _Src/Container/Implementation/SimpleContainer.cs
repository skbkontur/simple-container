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
		private readonly GenericsAutoCloser genericsAutoCloser;
		private readonly TypesList typesList;
		private readonly LogError errorLogger;
		private readonly LogInfo infoLogger;
		private readonly List<ImplementationSelector> implementationSelectors;
		private Type[] allTypes;

		internal readonly Dictionary<Type, Func<object, string>> valueFormatters;
		internal ConfigurationRegistry Configuration { get; private set; }

		public SimpleContainer(GenericsAutoCloser genericsAutoCloser, ConfigurationRegistry configurationRegistry,
			TypesList typesList, LogError errorLogger, LogInfo infoLogger,
			Dictionary<Type, Func<object, string>> valueFormatters)
		{
			Configuration = configurationRegistry;
			implementationSelectors = configurationRegistry.GetImplementationSelectors();
			this.genericsAutoCloser = genericsAutoCloser;
			this.typesList = typesList;
			dependenciesInjector = new DependenciesInjector(this);
			this.errorLogger = errorLogger;
			this.infoLogger = infoLogger;
			this.valueFormatters = valueFormatters;
		}

		public ResolvedService Resolve(Type type, IEnumerable<string> contracts)
		{
			EnsureNotDisposed();
			if (type == null)
				throw new ArgumentNullException("type");
			var name = CreateServiceName(type.UnwrapEnumerable(), contracts);
			var id = instanceCache.GetOrAdd(name, createId);
			ContainerService result;
			if (!id.TryGet(out result))
				result = ResolveSingleton(name.Type, new ResolutionContext(this, name.Contracts));
			return new ResolvedService(result, this, name.Type != type);
		}

		public object Create(Type type, IEnumerable<string> contracts, object arguments)
		{
			EnsureNotDisposed();
			if (type == null)
				throw new ArgumentNullException("type");
			var name = CreateServiceName(type, contracts);
			Func<object> compiledFactory;
			if (arguments == null && factoryCache.TryGetValue(name, out compiledFactory))
				return compiledFactory();
			var context = new ResolutionContext(this, name.Contracts);
			var result = context.Create(type, null, arguments);
			EnsureInitialized(result);
			return result.GetSingleValue(false, null);
		}

		private ServiceConfiguration GetConfigurationWithoutContracts(Type type)
		{
			return GetConfigurationOrNull(type, new List<string>());
		}

		internal ServiceConfiguration GetConfiguration(Type type, ResolutionContext context)
		{
			return GetConfigurationOrNull(type, context.Contracts) ?? ServiceConfiguration.empty;
		}

		internal void EnsureInitialized(ContainerService containerService)
		{
			containerService.EnsureInitialized(infoLogger);
		}

		private ServiceConfiguration GetConfigurationOrNull(Type type, List<string> contracts)
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
			return typesList.InheritorsOf(interfaceType)
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
			var contractsArray = contracts == null ? InternalHelpers.emptyStrings : contracts.ToArray();
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
			return ServiceName.Parse(type, true, contractsArray);
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
			return new SimpleContainer(genericsAutoCloser, Configuration.Apply(typesList, configure),
				typesList, null, infoLogger, valueFormatters);
		}

		public Type[] AllTypes
		{
			get
			{
				return allTypes ??
				       (allTypes = typesList.Types.Where(x => x.Assembly != typeof (SimpleContainer).Assembly).ToArray());
			}
		}

		internal ContainerService ResolveSingleton(Type type, ResolutionContext context)
		{
			var name = new ServiceName(type, context.Contracts.ToArray());
			var id = instanceCache.GetOrAdd(name, createId);
			ContainerService result;
			if (!id.AcquireInstantiateLock(out result))
				return result;
			result = context.Instantiate(type, false, null);
			id.ReleaseInstantiateLock(result);
			return result;
		}

		internal void Instantiate(ContainerService.Builder builder)
		{
			LifestyleAttribute lifestyle;
			if (builder.Type.IsSimpleType())
				builder.SetError("can't create simple type");
			else if (builder.Type == typeof (IContainer))
				builder.AddInstance(this, false);
			else if (builder.Configuration.ImplementationAssigned)
				builder.AddInstance(builder.Configuration.Implementation, builder.Configuration.ContainerOwnsInstance);
			else if (builder.Configuration.Factory != null)
				builder.CreateInstanceBy(() => builder.Configuration.Factory(this), builder.Configuration.ContainerOwnsInstance);
			else if (builder.Configuration.FactoryWithTarget != null)
			{
				var previousService = builder.Context.GetPreviousBuilder();
				var target = previousService == null ? null : previousService.Type;
				builder.CreateInstanceBy(() => builder.Configuration.FactoryWithTarget(this, target),
					builder.Configuration.ContainerOwnsInstance);
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

			if (builder.Configuration.InstanceFilter != null)
			{
				var filteredOutCount = builder.FilterInstances(builder.Configuration.InstanceFilter);
				if (filteredOutCount > 0)
					builder.SetComment("instance filter");
			}
		}

		private void InstantiateInterface(ContainerService.Builder builder)
		{
			var implementationTypes = GetInterfaceImplementationTypes(builder);
			List<ImplementationSelectorDecision> selectorDecisions = null;
			if (builder.HasNoConfiguration() && implementationSelectors.Count > 0)
			{
				selectorDecisions = new List<ImplementationSelectorDecision>();
				var typesArray = implementationTypes.ToArray();
				foreach (var s in implementationSelectors)
					s(builder.Type, typesArray, selectorDecisions);
				foreach (var decision in selectorDecisions)
					if (decision.action == ImplementationSelectorDecision.Action.Include)
						implementationTypes.Add(decision.target);
			}
			if (implementationTypes.Count == 0)
			{
				builder.SetComment("has no implementations");
				return;
			}
			Func<object> factory = null;
			var canUseFactory = true;
			foreach (var implementationType in implementationTypes)
			{
				ContainerService implementationService = null;
				string comment = null;

				var configuration = GetConfiguration(implementationType, builder.Context);
				if (configuration.IgnoredImplementation || implementationType.IsDefined("IgnoredImplementationAttribute"))
					comment = "IgnoredImplementation";
				else if (builder.CreateNew)
					implementationService = builder.Context.Instantiate(implementationType, true, builder.Arguments);
				else
				{
					ImplementationSelectorDecision? decision = null;
					if (selectorDecisions != null)
						foreach (var d in selectorDecisions)
							if (d.target == implementationType)
							{
								decision = d;
								break;
							}
					if (decision.HasValue)
						comment = decision.Value.comment;
					if (!decision.HasValue || decision.Value.action == ImplementationSelectorDecision.Action.Include)
						implementationService = builder.Context.Resolve(ServiceName.Parse(implementationType, false));
				}
				if (implementationService != null)
				{
					builder.LinkTo(implementationService, comment);
					if (builder.CreateNew && builder.Arguments == null &&
					    implementationService.Status == ServiceStatus.Ok && canUseFactory)
						if (factory == null)
						{
							if (!factoryCache.TryGetValue(implementationService.Name, out factory))
								canUseFactory = false;
						}
						else
							canUseFactory = false;
				}
				else
				{
					var dependency = ServiceDependency.NotResolved(null, implementationType.FormatName());
					dependency.Comment = comment;
					builder.AddDependency(dependency, true);
				}
				if (builder.Status.IsBad())
					return;
			}
			builder.EndResolveDependencies();
			if (factory != null && canUseFactory)
				factoryCache.TryAdd(builder.GetName(), factory);
		}

		private HashSet<Type> GetInterfaceImplementationTypes(ContainerService.Builder builder)
		{
			var candidates = new List<Type>();
			var result = new HashSet<Type>();
			if (builder.Configuration.ImplementationTypes != null)
				candidates.AddRange(builder.Configuration.ImplementationTypes);
			if (builder.Configuration.ImplementationTypes == null)
				candidates.AddRange(typesList.InheritorsOf(builder.Type.GetDefinition()));
			foreach (var implType in candidates)
			{
				if (!implType.IsGenericType)
				{
					if (!builder.Type.IsGenericType || builder.Type.IsAssignableFrom(implType))
						result.Add(implType);
				}
				else if (!implType.ContainsGenericParameters)
					result.Add(implType);
				else
				{
					var mapped = genericsAutoCloser.AutoCloseDefinition(implType);
					foreach (var type in mapped)
						if (builder.Type.IsAssignableFrom(type))
							result.Add(type);
					if (builder.Type.IsGenericType)
					{
						var implInterfaces = implType.ImplementationsOf(builder.Type.GetGenericTypeDefinition());
						foreach (var implInterface in implInterfaces)
						{
							var closed = implType.TryCloseByPattern(implInterface, builder.Type);
							if (closed != null)
								result.Add(closed);
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
								result.Add(closedItem);
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
				builder.AddInstance(result, true);
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
				if (builder.Status != ServiceStatus.Ok)
					return;
				actualArguments[i] = dependency.Value;
			}
			foreach (var d in builder.Configuration.ImplicitDependencies)
			{
				var dependency = builder.Context.Resolve(d).AsSingleInstanceDependency(null);
				dependency.Comment = "implicit";
				builder.AddDependency(dependency, false);
				if (dependency.ContainerService != null)
					builder.UnionUsedContracts(dependency.ContainerService);
				if (builder.Status != ServiceStatus.Ok)
					return;
			}
			builder.EndResolveDependencies();
			var dependenciesResolvedByArguments = builder.Arguments == null
				? InternalHelpers.emptyStrings
				: builder.Arguments.GetUsed().Select(InternalHelpers.ByNameDependencyKey);
			var unusedConfigurationKeys = builder.Configuration.GetUnusedDependencyConfigurationKeys()
				.Except(dependenciesResolvedByArguments)
				.ToArray();
			if (unusedConfigurationKeys.Length > 0)
			{
				builder.SetError(string.Format("unused dependency configurations [{0}]", unusedConfigurationKeys.JoinStrings(",")));
				return;
			}
			if (hasServiceNameParameters)
				for (var i = 0; i < formalParameters.Length; i++)
					if (formalParameters[i].ParameterType == typeof (ServiceName))
						actualArguments[i] = builder.GetName();
			if (builder.CreateNew || builder.DeclaredContracts.Length == builder.FinalUsedContracts.Length)
			{
				builder.CreateInstance(constructor.value, null, actualArguments);
				if (builder.CreateNew && builder.Arguments == null)
				{
					var compiledConstructor = constructor.value.Compile();
					factoryCache.TryAdd(builder.GetName(), () =>
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
			var serviceForUsedContractsId = instanceCache.GetOrAdd(builder.GetName(), createId);
			ContainerService serviceForUsedContracts;
			if (serviceForUsedContractsId.AcquireInstantiateLock(out serviceForUsedContracts))
			{
				builder.CreateInstance(constructor.value, null, actualArguments);
				serviceForUsedContracts = builder.Build();
				serviceForUsedContractsId.ReleaseInstantiateLock(serviceForUsedContracts);
			}
			else
				builder.Reuse(serviceForUsedContracts);
		}

		internal ServiceDependency InstantiateDependency(ParameterInfo formalParameter, ContainerService.Builder builder)
		{
			ValueWithType actualArgument;
			if (builder.Arguments != null && builder.Arguments.TryGet(formalParameter.Name, out actualArgument))
				return ServiceDependency.Constant(formalParameter, actualArgument.value);
			var parameters = builder.Configuration.ParametersSource;
			object actualParameter;
			if (parameters != null && parameters.TryGet(formalParameter.Name, formalParameter.ParameterType, out actualParameter))
				return ServiceDependency.Constant(formalParameter, actualParameter);
			var dependencyConfiguration = builder.Configuration.GetOrNull(formalParameter);
			Type implementationType = null;
			if (dependencyConfiguration != null)
			{
				if (dependencyConfiguration.ValueAssigned)
					return ServiceDependency.Constant(formalParameter, dependencyConfiguration.Value);
				if (dependencyConfiguration.Factory != null)
				{
					var dependencyBuilder = new ContainerService.Builder(formalParameter.ParameterType, builder.Context, false, null);
					dependencyBuilder.CreateInstanceBy(() => dependencyConfiguration.Factory(this), true);
					return dependencyBuilder.Build().AsSingleInstanceDependency(formalParameter.Name);
				}
				implementationType = dependencyConfiguration.ImplementationType;
			}
			implementationType = implementationType ?? formalParameter.ParameterType;
			FromResourceAttribute resourceAttribute;
			if (implementationType == typeof (Stream) && formalParameter.TryGetCustomAttribute(out resourceAttribute))
			{
				var resourceStream = builder.Type.Assembly.GetManifestResourceStream(builder.Type, resourceAttribute.Name);
				if (resourceStream == null)
					return ServiceDependency.Error(null, formalParameter.Name,
						"can't find resource [{0}] in namespace of [{1}], assembly [{2}]",
						resourceAttribute.Name, builder.Type, builder.Type.Assembly.GetName().Name);
				return ServiceDependency.Constant(formalParameter, resourceStream);
			}
			var dependencyName = ServiceName.Parse(implementationType.UnwrapEnumerable(), false,
				InternalHelpers.ParseContracts(formalParameter));

			ServiceConfiguration interfaceConfiguration;
			try
			{
				interfaceConfiguration = GetConfiguration(dependencyName.Type, builder.Context);
			}
			catch (Exception e)
			{
				var dependencyService = new ContainerService.Builder(dependencyName.Type, builder.Context, false, null);
				dependencyService.SetError(e);
				return ServiceDependency.ServiceError(dependencyService.Build());
			}
			if (interfaceConfiguration.FactoryWithTarget != null)
				dependencyName = dependencyName.AddContracts(builder.Type.FormatName());
			if (dependencyName.Type.IsSimpleType())
			{
				if (!formalParameter.HasDefaultValue)
					return ServiceDependency.Error(null, formalParameter.Name,
						"parameter [{0}] of service [{1}] is not configured",
						formalParameter.Name, builder.Type.FormatName());
				return ServiceDependency.Constant(formalParameter, formalParameter.DefaultValue);
			}
			var resultService = builder.Context.Resolve(dependencyName);
			if (resultService.Status.IsBad())
				return ServiceDependency.ServiceError(resultService);
			var isEnumerable = dependencyName.Type != implementationType;
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

		protected void EnsureNotDisposed()
		{
			if (disposed)
				throw new ObjectDisposedException("SimpleContainer");
		}
	}
}