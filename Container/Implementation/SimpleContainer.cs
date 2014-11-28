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
	public class SimpleContainer : IContainer
	{
		private static readonly ConcurrentDictionary<MethodBase, Func<object, object[], object>> compiledMethods =
			new ConcurrentDictionary<MethodBase, Func<object, object[], object>>();

		private static readonly Func<MethodBase, Func<object, object[], object>> compileMethodDelegate =
			ReflectionHelpers.EmitCallOf;

		private static readonly Func<CacheKey, ContainerService> createContainerServiceDelegate =
			k => new ContainerService {type = k.type};

		private static readonly IFactoryPlugin[] factoryPlugins =
		{
			new SimpleFactoryPlugin(),
			new FactoryWithArgumentsPlugin()
		};

		protected readonly IContainerConfiguration configuration;
		protected readonly IInheritanceHierarchy inheritors;
		private readonly SimpleContainer staticContainer;
		private readonly CacheLevel cacheLevel;

		private readonly ConcurrentDictionary<CacheKey, ContainerService> instanceCache =
			new ConcurrentDictionary<CacheKey, ContainerService>();

		private readonly Func<CacheKey, ContainerService> createInstanceDelegate;
		private readonly DependenciesInjector dependenciesInjector;
		private int topSortIndex;

		internal SimpleContainer(IContainerConfiguration configuration, IInheritanceHierarchy inheritors,
			SimpleContainer staticContainer)
		{
			this.configuration = configuration;
			this.inheritors = inheritors;
			this.staticContainer = staticContainer;
			cacheLevel = staticContainer == null ? CacheLevel.Static : CacheLevel.Local;
			dependenciesInjector = new DependenciesInjector(this);
			createInstanceDelegate = delegate(CacheKey key)
			{
				var context = new ResolutionContext(configuration, key.contract);
				return ResolveSingleton(key.type, null, context);
			};
		}

		public object Get(Type serviceType, string contract)
		{
			Type enumerableItem;
			if (TryUnwrapEnumerable(serviceType, out enumerableItem))
				return GetAll(enumerableItem);
			var key = new CacheKey(serviceType, contract);
			return instanceCache.GetOrAdd(key, createInstanceDelegate).WaitForResolve().SingleInstance();
		}

		public IEnumerable<object> GetAll(Type serviceType)
		{
			return instanceCache.GetOrAdd(new CacheKey(serviceType, null), createInstanceDelegate)
				.WaitForResolve()
				.AsEnumerable();
		}

		public object Create(Type type, string contract, object arguments)
		{
			var resolutionContext = new ResolutionContext(configuration, contract);
			var result = new ContainerService
			{
				type = type,
				arguments = ObjectAccessor.Get(arguments),
				createNew = true
			};
			resolutionContext.Resolve(null, result, this);
			if (result.arguments != null)
			{
				var unused = result.arguments.GetUnused().ToArray();
				if (unused.Any())
					resolutionContext.Throw("arguments [{0}] are not used", unused.JoinStrings(","));
			}
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

		public IEnumerable<object> GetInstanceCache(Type type)
		{
			var resultServices = instanceCache.Values.Where(x => !x.type.IsAbstract && type.IsAssignableFrom(x.type));
			var result = new List<ContainerService>(resultServices);
			result.Sort((a, b) => a.topSortIndex.CompareTo(b.topSortIndex));
			return result.SelectMany(x => x.instances);
		}

		public IContainer Clone()
		{
			return new SimpleContainer(configuration, inheritors, staticContainer);
		}

		private ContainerService ResolveSingleton(Type type, string name, ResolutionContext context)
		{
			var serviceCacheLevel = GetCacheLevel(type, context);
			if (serviceCacheLevel == CacheLevel.Static && cacheLevel == CacheLevel.Local)
				return staticContainer.ResolveSingleton(type, name, context);
			if (serviceCacheLevel == CacheLevel.Local && cacheLevel == CacheLevel.Static)
				context.Throw("local service [{0}] can't be resolved in static context", type.FormatName());
			var cacheKey = new CacheKey(type, context.Contract);
			var result = instanceCache.GetOrAdd(cacheKey, createContainerServiceDelegate);
			if (!result.resolved)
				lock (result.lockObject)
					if (!result.resolved)
					{
						context.Resolve(name, result, this);
						result.SetResolved();
						result.topSortIndex = Interlocked.Increment(ref topSortIndex);
					}
			return result;
		}

		private static CacheLevel GetCacheLevel(Type type, ResolutionContext context)
		{
			var interfaceConfiguration = context.GetConfiguration<InterfaceConfiguration>(type);
			if (interfaceConfiguration != null && interfaceConfiguration.CacheLevel.HasValue)
				return interfaceConfiguration.CacheLevel.Value;
			return type.IsDefined<StaticAttribute>() ? CacheLevel.Static : CacheLevel.Local;
		}

		internal void Instantiate(ContainerService service)
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
					service.instances.Add(interfaceConfiguration.Factory(new FactoryContext
					{
						container = this,
						contract = service.context.Contract
					}));
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
				ContainerService childService;
				if (service.createNew)
				{
					childService = new ContainerService
					{
						type = implementationType,
						arguments = service.arguments
					};
					service.context.Resolve(null, childService, this);
				}
				else
					childService = ResolveSingleton(implementationType, null, service.context);
				service.instances.AddRange(childService.instances);
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
			var factory = ResolveSingleton(factoryMethod.DeclaringType, null, service.context);
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
				var dependencyService = InstantiateDependency(formalParameter, implementation, service);
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
			ContainerService service)
		{
			object actualArgument;
			if (service.arguments != null && service.arguments.TryGet(formalParameter.Name, out actualArgument))
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
							implementation.type.Assembly.GetName().Name, service.context.Format()));
				return ResolvedService(resourceStream);
			}
			Type enumerableItem;
			var isEnumerable = TryUnwrapEnumerable(implementationType, out enumerableItem);
			var dependencyType = isEnumerable ? enumerableItem : implementationType;

			RequireContractAttribute requireContractAttribute;
			var contracts = formalParameter.TryGetCustomAttribute(out requireContractAttribute) ||
			                dependencyType.TryGetCustomAttribute(out requireContractAttribute)
				? new List<string>(1) {requireContractAttribute.ContractName}
				: (dependencyConfiguration != null && dependencyConfiguration.Contracts != null
					? dependencyConfiguration.Contracts
					: null);

			var interfaceConfiguration = service.context.GetConfiguration<InterfaceConfiguration>(implementationType);
			if (interfaceConfiguration != null && interfaceConfiguration.Factory != null)
			{
				var instance = interfaceConfiguration.Factory(new FactoryContext
				{
					container = this,
					target = implementation.type,
					contract = contracts != null ? contracts.SingleOrDefault() : null
				});
				return ResolvedService(instance);
			}

			ContainerService result;
			if (contracts != null)
			{
				result = new ContainerService {type = dependencyType};
				var contractServices = contracts
					.Select(delegate(string contract)
					{
						service.context.ActivateContract(contract);
						var childService = ResolveSingleton(dependencyType, formalParameter.Name, service.context);
						service.context.DeactivateContract();
						return childService;
					});
				result.instances.AddRange(contractServices.SelectMany(x => x.instances).Distinct());
			}
			else
				result = ResolveSingleton(dependencyType, formalParameter.Name, service.context);
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
			public readonly string contract;

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

		public void Dispose()
		{
			var exceptions = new List<SimpleContainerException>();
			foreach (var disposable in this.GetInstanceCache<IDisposable>().Where(x => !ReferenceEquals(x, this)).Reverse())
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
	}
}