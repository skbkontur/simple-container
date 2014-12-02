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

		public SimpleContainer(IContainerConfiguration configuration, IInheritanceHierarchy inheritors,
			SimpleContainer staticContainer)
		{
			this.configuration = configuration;
			this.inheritors = inheritors;
			this.staticContainer = staticContainer;
			cacheLevel = staticContainer == null ? CacheLevel.Static : CacheLevel.Local;
			dependenciesInjector = new DependenciesInjector(this);
			createInstanceDelegate = delegate(CacheKey key)
			{
				var context = new ResolutionContext(configuration, key.contracts);
				return ResolveSingleton(key.type, null, context);
			};
		}

		public object Get(Type serviceType, IEnumerable<string> contracts)
		{
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
			result.WaitForResolve();
			return result.Failed ? createInstanceDelegate(cacheKey) : result;
		}

		public IEnumerable<object> GetAll(Type serviceType)
		{
			return GetInternal(new CacheKey(serviceType, null)).AsEnumerable();
		}

		public object Create(Type type, IEnumerable<string> contracts, object arguments)
		{
			var resolutionContext = new ResolutionContext(configuration, contracts);
			var result = new ContainerService
			{
				type = type,
				arguments = ObjectAccessor.Get(arguments),
				createNew = true
			};
			resolutionContext.Instantiate(null, result, this);
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
			dependenciesInjector.BuildUp(target);
		}

		public void DumpConstructionLog(Type type, IEnumerable<string> contracts, bool entireResolutionContext,
			ISimpleLogWriter writer)
		{
			ContainerService containerService;
			if (instanceCache.TryGetValue(new CacheKey(type, contracts), out containerService))
				containerService.context.Format(entireResolutionContext ? null : type, writer);
		}

		public IEnumerable<object> GetInstanceCache(Type type)
		{
			var resultServices = instanceCache.Values.Where(x => !x.type.IsAbstract && type.IsAssignableFrom(x.type));
			var result = new List<ContainerService>(resultServices);
			result.Sort((a, b) => a.TopSortIndex.CompareTo(b.TopSortIndex));
			return result.SelectMany(x => x.Instances).Distinct();
		}

		public IContainer Clone()
		{
			return new SimpleContainer(configuration, inheritors, staticContainer);
		}

		public ContainerService ResolveSingleton(Type type, string name, ResolutionContext context)
		{
			var serviceCacheLevel = GetCacheLevel(type, context);
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

		private static CacheLevel GetCacheLevel(Type type, ResolutionContext resolutionContext)
		{
			var interfaceConfiguration = resolutionContext.GetInitialContainerConfiguration<InterfaceConfiguration>(type);
			if (interfaceConfiguration != null && interfaceConfiguration.CacheLevel.HasValue)
				return interfaceConfiguration.CacheLevel.Value;
			return type.IsDefined<StaticAttribute>() ? CacheLevel.Static : CacheLevel.Local;
		}

		public void Instantiate(ContainerService service)
		{
			if (ReflectionHelpers.simpleTypes.Contains(service.type))
				service.Throw("can't create simple type");
			if (service.type == typeof (IContainer))
			{
				service.AddInstance(this);
				return;
			}
			var interfaceConfiguration = service.context.GetConfiguration<InterfaceConfiguration>(service.type);
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
						contracts = service.context.RequiredContractNames()
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
			if (service.type.IsAbstract)
				InstantiateInterface(service, implementationTypes, useAutosearch);
			else
				InstantiateImplementation(service);
		}

		private void InstantiateInterface(ContainerService service, IEnumerable<Type> implementationTypes, bool useAutosearch)
		{
			IEnumerable<Type> localTypes;
			if (implementationTypes == null)
				localTypes = GetInheritors(service.type);
			else if (useAutosearch)
				localTypes = implementationTypes.Union(GetInheritors(service.type));
			else
				localTypes = implementationTypes;
			var localTypesArray = localTypes.ToArray();
			if (localTypesArray.Length == 0)
			{
				service.context.Report("has no implementations");
				return;
			}
			foreach (var implementationType in localTypesArray)
			{
				ContainerService childService;
				if (service.createNew)
				{
					childService = new ContainerService
					{
						type = implementationType,
						arguments = service.arguments
					};
					service.context.Instantiate(null, childService, this);
				}
				else
					childService = ResolveSingleton(implementationType, null, service.context);
				service.UnionFrom(childService);
			}
			service.EndResolveDependencies();
		}

		private void InstantiateImplementation(ContainerService service)
		{
			if (service.type.IsDefined<IgnoreImplementationAttribute>())
				return;
			var implementationConfiguration = service.context.GetConfiguration<ImplementationConfiguration>(service.type);
			if (implementationConfiguration != null && implementationConfiguration.DontUseIt)
				return;
			var factoryMethod = GetFactoryOrNull(service.type);
			if (factoryMethod == null)
				DefaultInstantiateImplementation(service.type, service);
			else
			{
				var factory = ResolveSingleton(factoryMethod.DeclaringType, null, service.context);
				if (factory.Instances.Count == 1)
				{
					var instance = InvokeConstructor(factoryMethod, factory.Instances[0], new object[0], service.context);
					service.AddInstance(instance);
				}
			}
			if (implementationConfiguration != null && implementationConfiguration.InstanceFilter != null)
				service.FilterInstances(implementationConfiguration.InstanceFilter);
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
			implementation.SetContext(service.context);
			var formalParameters = constructor.GetParameters();
			var actualArguments = new object[formalParameters.Length];
			for (var i = 0; i < formalParameters.Length; i++)
			{
				var formalParameter = formalParameters[i];
				var dependencyService = InstantiateDependency(formalParameter, implementation, service);
				service.UnionUsedContracts(dependencyService);
				if (dependencyService.Instances.Count == 0)
				{
					service.EndResolveDependencies();
					return;
				}
				var dependencyValue = dependencyService.SingleInstance();
				if (dependencyValue != null && !formalParameter.ParameterType.IsInstanceOfType(dependencyValue))
					service.Throw("can't cast [{0}] to [{1}] for dependency [{2}] with value [{3}]",
						dependencyValue.GetType().FormatName(),
						formalParameter.ParameterType.FormatName(),
						formalParameter.Name,
						dependencyValue);
				actualArguments[i] = dependencyValue;
			}
			service.EndResolveDependencies();
			var unusedDependencyConfigurations = implementation.GetUnusedDependencyConfigurationNames().ToArray();
			if (unusedDependencyConfigurations.Length > 0)
				service.Throw("unused dependency configurations [{0}]", unusedDependencyConfigurations.JoinStrings(","));
			if (service.FinalUsedContracts.Length == service.context.requiredContracts.Count)
			{
				var instance = InvokeConstructor(constructor, null, actualArguments, service.context);
				service.AddInstance(instance);
				return;
			}
			var usedContactsCacheKey = new CacheKey(type, service.FinalUsedContracts);
			var serviceForUsedContracts = instanceCache.GetOrAdd(usedContactsCacheKey, createContainerServiceDelegate);
			if (serviceForUsedContracts.AcquireInstantiateLock())
				try
				{
					serviceForUsedContracts.context = service.context;
					var instance = InvokeConstructor(constructor, null, actualArguments, service.context);
					serviceForUsedContracts.AddInstance(instance);
					serviceForUsedContracts.InstantiatedSuccessfully(Interlocked.Increment(ref topSortIndex));
				}
				catch
				{
					serviceForUsedContracts.InstantiatedUnsuccessfully();
				}
				finally
				{
					serviceForUsedContracts.ReleaseInstantiateLock();
				}
			foreach (var instance in serviceForUsedContracts.Instances)
				service.AddInstance(instance);
		}

		public ContainerService IndependentService(object instance)
		{
			var result = new ContainerService();
			result.AddInstance(instance);
			return result;
		}

		public ContainerService DependentService(object instance, ContainerService dependency)
		{
			var result = new ContainerService();
			result.AddInstance(instance);
			result.UnionUsedContracts(dependency);
			return result;
		}

		private ContainerService InstantiateDependency(ParameterInfo formalParameter, Implementation implementation,
			ContainerService service)
		{
			object actualArgument;
			if (service.arguments != null && service.arguments.TryGet(formalParameter.Name, out actualArgument))
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
							implementation.type.Assembly.GetName().Name, service.context.Format()));
				return IndependentService(resourceStream);
			}
			Type enumerableItem;
			var isEnumerable = TryUnwrapEnumerable(implementationType, out enumerableItem);
			var dependencyType = isEnumerable ? enumerableItem : implementationType;

			RequireContractAttribute requireContractAttribute;
			var baseContractName = formalParameter.TryGetCustomAttribute(out requireContractAttribute) ||
			                       dependencyType.TryGetCustomAttribute(out requireContractAttribute)
				? requireContractAttribute.ContractName
				: null;

			var interfaceConfiguration = service.context.GetConfiguration<InterfaceConfiguration>(implementationType);
			if (interfaceConfiguration != null && interfaceConfiguration.Factory != null)
			{
				var contacts = new List<string>(service.context.RequiredContractNames());
				if (baseContractName != null)
					contacts.Add(baseContractName);
				var instance = interfaceConfiguration.Factory(new FactoryContext
				{
					container = this,
					target = implementation.type,
					contracts = contacts
				});
				return IndependentService(instance);
			}

			ContainerService result;
			if (baseContractName != null)
			{
				var contractServices = service.context.ResolveUsingContract(dependencyType, formalParameter.Name,
					baseContractName, this);
				result = new ContainerService {type = dependencyType, context = service.context};
				foreach (var contractService in contractServices)
					result.UnionFrom(contractService);
			}
			else
				result = ResolveSingleton(dependencyType, formalParameter.Name, service.context);
			if (isEnumerable)
				return DependentService(result.AsEnumerable(), result);
			if (result.Instances.Count == 0 && formalParameter.HasDefaultValue)
				result.AddInstance(formalParameter.DefaultValue);
			if (result.Instances.Count == 0 && formalParameter.IsDefined<OptionalAttribute>())
				result.AddInstance(null);
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
			private readonly string contractsKey;

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