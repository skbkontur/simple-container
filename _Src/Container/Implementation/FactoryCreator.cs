using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Annotations;
using SimpleContainer.Helpers;
using SimpleContainer.Interface;

namespace SimpleContainer.Implementation
{
	internal static class FactoryCreator
	{
		private static readonly ILookup<Type, MethodInfo> signatures = typeof (SupportedSignatures)
			.GetMethods(BindingFlags.Static | BindingFlags.Public)
			.ToLookup(x => x.ReturnType.GetGenericTypeDefinition());

		private static readonly ConcurrentDictionary<SignatureDelegateKey, Delegate> casters =
			new ConcurrentDictionary<SignatureDelegateKey, Delegate>();

		private static readonly Func<SignatureDelegateKey, Delegate> createCaster = delegate(SignatureDelegateKey key)
		{
			var delegateType = typeof (Func<Func<Type, object, object>, object>);
			return Delegate.CreateDelegate(delegateType, key.signature.MakeGenericMethod(key.resultType));
		};

		public static object TryCreate(ContainerService.Builder builder)
		{
			var funcType = builder.Type;
			if (!funcType.IsGenericType || !funcType.IsDelegate())
				return null;
			Type resultType;
			var signature = FindSignature(funcType, out resultType);
			if (signature == null)
				return null;
			var factory = CreateFactory(builder, resultType);
			var caster = casters.GetOrAdd(new SignatureDelegateKey(resultType, signature), createCaster);
			var typedCaster = (Func<Func<Type, object, object>, object>) caster;
			return typedCaster(factory);
		}

		private static MethodInfo FindSignature(Type funcType, out Type resultType)
		{
			var funcArgumentTypes = funcType.GetGenericArguments();
			foreach (var signature in signatures[funcType.GetGenericTypeDefinition()])
				if (signature.ReturnType.GetGenericArguments().SameAs(funcArgumentTypes, funcArgumentTypes.Length - 1))
				{
					resultType = funcArgumentTypes[funcArgumentTypes.Length - 1];
					return signature;
				}
			resultType = null;
			return null;
		}

		private static Func<Type, object, object> CreateFactory(ContainerService.Builder builder, Type resultType)
		{
			var container = builder.Context.container;
			var factoryContractsArray = builder.DeclaredContracts;
			var factoryContracts = new List<string>(factoryContractsArray);
			var oldValue = builder.Context.analizeDependenciesOnly;
			builder.Context.analizeDependenciesOnly = true;
			var containerService = builder.Context.container.ResolveCore(new ServiceName(resultType), true, null,
				builder.Context);
			builder.Context.analizeDependenciesOnly = oldValue;
			builder.UnionUsedContracts(containerService);
			return delegate(Type type, object arguments)
			{
				var current = ContainerService.Builder.Current;
				if (current == null)
					return container.Create(type, factoryContractsArray, arguments);
				var oldContracts = current.Context.contracts.Replace(factoryContracts);
				var result = current.Context.container.ResolveCore(new ServiceName(type), true,
					ObjectAccessor.Get(arguments), current.Context);
				current.Context.contracts.Restore(oldContracts);
				var resultDependency = result.AsSingleInstanceDependency("() => " + result.Type.FormatName());
				current.AddDependency(resultDependency, false);
				if (resultDependency.Status != ServiceStatus.Ok)
					throw new ServiceCouldNotBeCreatedException();
				return resultDependency.Value;
			};
		}

		private static class SupportedSignatures
		{
			[UsedImplicitly]
			public static Func<T> WithoutArguments<T>(Func<Type, object, object> f)
			{
				return () => (T) f(typeof (T), null);
			}

			[UsedImplicitly]
			public static Func<object, T> WithArguments<T>(Func<Type, object, object> f)
			{
				return o => (T) f(typeof (T), o);
			}

			[UsedImplicitly]
			public static Func<Type, object, T> WithTypeAndArguments<T>(Func<Type, object, object> f)
			{
				return (t, o) => (T) f(t, o);
			}

			[UsedImplicitly]
			public static Func<object, Type, T> WithArgumentsAndType<T>(Func<Type, object, object> f)
			{
				return (o, t) => (T) f(t, o);
			}

			[UsedImplicitly]
			public static Func<Type, T> WithType<T>(Func<Type, object, object> f)
			{
				return t => (T) f(t, null);
			}
		}

		private struct SignatureDelegateKey : IEquatable<SignatureDelegateKey>
		{
			public readonly Type resultType;
			public readonly MethodInfo signature;

			public SignatureDelegateKey(Type resultType, MethodInfo signature)
			{
				this.resultType = resultType;
				this.signature = signature;
			}

			public bool Equals(SignatureDelegateKey other)
			{
				return resultType == other.resultType && signature.Equals(other.signature);
			}

			public override bool Equals(object obj)
			{
				if (ReferenceEquals(null, obj)) return false;
				return obj is SignatureDelegateKey && Equals((SignatureDelegateKey) obj);
			}

			public override int GetHashCode()
			{
				unchecked
				{
					return (resultType.GetHashCode()*397) ^ signature.GetHashCode();
				}
			}
		}
	}
}