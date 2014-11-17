using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SimpleContainer.Factories;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;
using SimpleContainer.Reflection;

namespace SimpleContainer
{
	public static class StupidContainerHelpers
	{
		public static SimpleContainer CreateContainer(IEnumerable<Type> types, params Action<ContainerConfigurationBuilder>[] configure)
		{
			var typesArray = types.ToArray();
			var configuration = SimpleContainerHelpers.CreateContainerConfiguration(typesArray, configure);
			var inheritors = DefaultInheritanceHierarchy.Create(typesArray);
			return new SimpleContainer(configuration, inheritors);
		}
	}

	//todo отпилить этап анализа зависимостей => meta, contractUsage + factories
	//todo перевести на явную стек машину
	//todo заинлайнить GenericConfigurator
	//todo обработка запросов на явные generic-и
	//todo логировать значения simple-типов, заюзанные через конфигурирование
	public class SimpleContainer : IContainer, IResolveDependency, IServiceFactory
	{
		private readonly IContainerConfiguration configuration;
		private readonly ConcurrentDictionary<CacheKey, ContainerService> instanceCache =
			new ConcurrentDictionary<CacheKey, ContainerService>();

		private static readonly ConcurrentDictionary<MethodBase, Func<object, object[], object>> compiledMethods =
			new ConcurrentDictionary<MethodBase, Func<object, object[], object>>();

		private readonly IInheritanceHierarchy inheritors;
		internal event Action<ContainerService> OnResolve;

		public SimpleContainer(IContainerConfiguration configuration, IInheritanceHierarchy inheritors)
		{
			this.configuration = configuration;
			this.inheritors = inheritors;
			dependenciesInjector = new DependenciesInjector(this);
			createInstanceDelegate = delegate(CacheKey key)
			{
				var context = new ResolutionContext(configuration);
				return Resolve(new ResolutionRequest {type = key.type}, context);
			};
		}

		private readonly Func<CacheKey, ContainerService> createInstanceDelegate;

		private static readonly Func<CacheKey, ContainerService> createContainerServiceDelegate =
			k => new ContainerService {type = k.type};

		private static readonly Func<MethodBase, Func<object, object[], object>> compileMethodDelegate =
			ReflectionHelpers.EmitCallOf;

		private readonly DependenciesInjector dependenciesInjector;

		public object Get(Type type)
		{
			return Get(type, null);
		}

		public object Get(Type serviceType, string contract)
		{
			Type enumerableItem;
			if (TryUnwrapEnumerable(serviceType, out enumerableItem))
				return GetAll(enumerableItem);
			var key = new CacheKey(serviceType, contract);
			var result = serviceType.IsDefined<DontReuseAttribute>()
				? createInstanceDelegate(key)
				: instanceCache.GetOrAdd(key, createInstanceDelegate).WaitForResolve();
			return result.SingleInstance();
		}

		public IEnumerable<object> GetAll(Type serviceType)
		{
			return instanceCache.GetOrAdd(new CacheKey(serviceType, null), createInstanceDelegate)
				.WaitForResolve()
				.AsEnumerable();
		}

		public object Create(Type type, string contract, object arguments)
		{
			var resolutionContext = new ResolutionContext(configuration, arguments);
			resolutionContext.ActivateContract(contract);
			var result = Resolve(new ResolutionRequest {type = type, createNew = true}, resolutionContext);
			return result.SingleInstance();
		}

		public IEnumerable<Type> GetImplementationsOf(Type interfaceType)
		{
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
			dependenciesInjector.BuildUp(target);
		}

		public void DumpConstructionLog(Type type, string contractName, bool entireResolutionContext, ISimpleLogWriter writer)
		{
			ContainerService containerService;
			if (instanceCache.TryGetValue(new CacheKey(type, contractName), out containerService))
				containerService.context.Format(entireResolutionContext ? null : type, writer);
		}

		private ContainerService Resolve(ResolutionRequest request, ResolutionContext context)
		{
			ContainerService result;
			var cacheKey = new CacheKey(request.type, context.Contract);
			if (request.createNew || request.type.IsDefined<DontReuseAttribute>())
			{
				result = createContainerServiceDelegate(cacheKey);
				DoResolve(request.name, result, context);
			}
			else
			{
				result = instanceCache.GetOrAdd(cacheKey, createContainerServiceDelegate);
				if (!result.resolved)
					lock (result.lockObject)
						if (!result.resolved)
						{
							DoResolve(request.name, result, context);
							result.SetResolved();
						}
			}
			return result;
		}

		private void DoResolve(string name, ContainerService containerService, ResolutionContext context)
		{
			context.BeginResolve(name, containerService);
			Instantiate(containerService);
			context.EndResolve(containerService);
		}

		private readonly IFactoryPlugin[] factoryPlugins =
		{
			new SimpleFactoryPlugin(),
			new FactoryWithArgumentsPlugin()
		};

		private void Instantiate(ContainerService service)
		{
			if (ReflectionHelpers.simpleTypes.Contains(service.type))
				service.Throw("can't create simple type");
			if (service.type == typeof (IContainer))
			{
				service.instances.Add(this);
				return;
			}
			var interfaceConfiguration = service.context.GetConfiguration<InterfaceConfiguration>(service.type);
			IEnumerable<Type> implementationTypes = null;
			var useAutosearch = false;
			if (interfaceConfiguration != null)
			{
				if (interfaceConfiguration.Implementation != null)
				{
					service.instances.Add(interfaceConfiguration.Implementation);
					return;
				}
				if (interfaceConfiguration.Factory != null)
				{
					service.instances.Add(interfaceConfiguration.Factory(new FactoryContext {Container = this}));
					return;
				}
				implementationTypes = interfaceConfiguration.ImplementationTypes;
				useAutosearch = interfaceConfiguration.UseAutosearch;
			}
			if (service.type.IsValueType)
				service.Throw("can't create value type");
			if (factoryPlugins.Any(p => p.TryInstantiate(this, service)))
				return;
			if (service.type.IsGenericType && service.type.ContainsGenericParameters)
			{
				service.context.Report("has open generic arguments");
				return;
			}
			IEnumerable<Type> localTypes;
			if (implementationTypes == null)
				localTypes = GetInheritors(service.type);
			else if (useAutosearch)
				localTypes = implementationTypes.Union(GetInheritors(service.type));
			else
				localTypes = implementationTypes;
			foreach (var type in localTypes)
				InstantiateImplementation(service, type);
		}

		private void InstantiateImplementation(ContainerService service, Type implementationType)
		{
			if (service.type != implementationType)
			{
				var implementationRequest = new ResolutionRequest
				{
					type = implementationType,
					createNew = service.type.IsDefined<DontReuseAttribute>()
				};
				var implementationService = Resolve(implementationRequest, service.context);
				service.instances.AddRange(implementationService.instances);
				return;
			}
			if (implementationType.IsDefined<IgnoreImplementationAttribute>())
				return;
			var implementationConfiguration = service.context.GetConfiguration<ImplementationConfiguration>(implementationType);
			if (implementationConfiguration != null && implementationConfiguration.DontUseIt)
				return;
			var factoryMethod = GetFactoryOrNull(implementationType);
			if (factoryMethod == null)
			{
				DefaultInstantiateImplementation(implementationType, service);
				return;
			}
			var factory = Resolve(new ResolutionRequest {type = factoryMethod.DeclaringType}, service.context);
			if (factory.instances.Count == 1)
				service.instances.Add(InvokeConstructor(factoryMethod, factory.instances[0], new object[0], service.context));
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
			private object arguments;
			private IObjectAccessor objectAccessor;

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
				arguments = context.arguments;
				objectAccessor = context.arguments == null
					? null
					: ObjectAccessors.Instance.GetAccessor(context.arguments.GetType());
			}

			public bool TryGetFromArguments(string name, out object result)
			{
				result = null;
				return objectAccessor != null && objectAccessor.TryGet(arguments, name, out result);
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
		}

		private void DefaultInstantiateImplementation(Type type, ContainerService service)
		{
			var implementation = new Implementation(type);
			ConstructorInfo constructor;
			if (!implementation.TryGetConstructor(out constructor))
				service.Throw(implementation.publicConstructors.Length == 0
					? "no public ctors, maybe ctor is private?"
					: "many public ctors, maybe some of them should be made private?");
			implementation.SetContext(service.context);
			var formalParameters = constructor.GetParameters();
			var actualArguments = new object[formalParameters.Length];
			for (var i = 0; i < formalParameters.Length; i++)
			{
				var formalParameter = formalParameters[i];
				var dependencyService = InstantiateDependency(formalParameter, implementation, service.context);
				service.contractUsed |= dependencyService.contractUsed;
				if (dependencyService.instances.Count == 0)
					return;
				var dependencyValue = dependencyService.SingleInstance();
				if (dependencyValue != null && !formalParameter.ParameterType.IsInstanceOfType(dependencyValue))
					service.Throw("can't cast [{0}] to [{1}] for dependency [{2}] with value [{3}]\r\n{4}",
						dependencyValue.GetType().FormatName(),
						formalParameter.ParameterType.FormatName(),
						formalParameter.Name,
						dependencyValue,
						service.context.Format());
				actualArguments[i] = dependencyValue;
			}
			if (service.context.Contract == null || service.contractUsed)
			{
				service.instances.Add(InvokeConstructor(constructor, null, actualArguments, service.context));
				return;
			}
			var serviceWithoutContract = instanceCache.GetOrAdd(new CacheKey(type, null), createContainerServiceDelegate);
			if (!serviceWithoutContract.resolved)
				lock (serviceWithoutContract.lockObject)
					if (!serviceWithoutContract.resolved)
					{
						serviceWithoutContract.context = service.context;
						serviceWithoutContract.instances.Add(InvokeConstructor(constructor, null, actualArguments, service.context));
						serviceWithoutContract.SetResolved();
					}
			service.instances.AddRange(serviceWithoutContract.instances);
		}

		public ContainerService ResolvedService(object instance, bool contractUsed = false)
		{
			var result = new ContainerService {contractUsed = contractUsed};
			result.instances.Add(instance);
			return result;
		}

		private ContainerService InstantiateDependency(ParameterInfo formalParameter, Implementation implementation,
			ResolutionContext resolutionContext)
		{
			object actualArgument;
			if (implementation.TryGetFromArguments(formalParameter.Name, out actualArgument))
				return ResolvedService(actualArgument);
			var dependencyConfiguration = implementation.GetDependencyConfiguration(formalParameter);
			Type implementationType = null;
			if (dependencyConfiguration != null)
			{
				if (dependencyConfiguration.ValueAssigned)
					return ResolvedService(dependencyConfiguration.Value);
				if (dependencyConfiguration.Factory != null)
					return ResolvedService(dependencyConfiguration.Factory(this));
				implementationType = dependencyConfiguration.ImplementationType;
			}
			if (implementationType == null)
			{
				var interfaceConfiguration =
					resolutionContext.GetConfiguration<InterfaceConfiguration>(formalParameter.ParameterType);
				if (interfaceConfiguration != null && interfaceConfiguration.Factory != null)
				{
					var instance = interfaceConfiguration.Factory(new FactoryContext {Container = this, Target = implementation.type});
					return ResolvedService(instance);
				}
			}
			implementationType = implementationType ?? formalParameter.ParameterType;
			if (ReflectionHelpers.simpleTypes.Contains(implementationType) && formalParameter.HasDefaultValue)
				return ResolvedService(formalParameter.DefaultValue);
			FromResourceAttribute resourceAttribute;
			if (implementationType == typeof (Stream) && formalParameter.TryGetCustomAttribute(out resourceAttribute))
			{
				var resourceStream = implementation.type.Assembly.GetManifestResourceStream(implementation.type,
					resourceAttribute.Name);
				if (resourceStream == null)
					throw new SimpleContainerException(
						string.Format("can't find resource [{0}] in namespace of [{1}], assembly [{2}]\r\n{3}",
							resourceAttribute.Name, implementation.type,
							implementation.type.Assembly.GetName().Name, resolutionContext.Format()));
				return ResolvedService(resourceStream);
			}
			Type enumerableItem;
			var isEnumerable = TryUnwrapEnumerable(implementationType, out enumerableItem);
			var childRequest = new ResolutionRequest
			{
				type = isEnumerable ? enumerableItem : implementationType,
				name = formalParameter.Name
			};

			RequireContractAttribute requireContractAttribute;
			var contracts = formalParameter.TryGetCustomAttribute(out requireContractAttribute)
				? new[] {requireContractAttribute.ContractName}
				: (dependencyConfiguration != null && dependencyConfiguration.Contracts != null
					? (IEnumerable<string>) dependencyConfiguration.Contracts
					: null);
			ContainerService result;
			if (contracts != null)
			{
				result = new ContainerService {type = childRequest.type};
				var contractServices = new List<object>();
				foreach (var contract in contracts)
				{
					resolutionContext.ActivateContract(contract);
					contractServices.AddRange(Resolve(childRequest, resolutionContext).instances);
					resolutionContext.DeactivateContext();
				}
				result.instances.AddRange(contractServices.Distinct());
			}
			else
				result = Resolve(childRequest, resolutionContext);
			if (isEnumerable)
				return ResolvedService(result.AsEnumerable(), result.contractUsed);
			if (result.instances.Count == 0 && formalParameter.HasDefaultValue)
				result.instances.Add(formalParameter.DefaultValue);
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
			private readonly string contract;

			public CacheKey(Type type, string contract)
			{
				this.type = type;
				this.contract = contract ?? "";
			}

			public bool Equals(CacheKey other)
			{
				return type == other.type && contract == other.contract;
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
					return (type.GetHashCode()*397) ^ contract.GetHashCode();
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
	}
}