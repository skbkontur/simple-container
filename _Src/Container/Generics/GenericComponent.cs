using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Configuration;

namespace SimpleContainer.Generics
{
	internal class GenericComponent
	{
		public GenericComponent(Type type, IEnumerable<Type> genericDependencies, Type[] genericConstraint)
		{
			Type = type;
			GenericConstraint = genericConstraint;
			GenericDependencies = genericDependencies.ToList();
			Overrides = new List<GenericOverrideInfo>();
			DependenciesToClose = new List<GenericDependency>();
		}

		public Type Type { get; private set; }
		public Type[] GenericConstraint { get; private set; }
		public List<Type> GenericDependencies { get; private set; }
		public List<GenericDependency> DependenciesToClose { get; private set; }
		public List<GenericOverrideInfo> Overrides { get; private set; }

		public bool SatisfyConstraints(Type type)
		{
			if (GenericConstraint.Any(c => !c.IsAssignableFrom(type)))
				return false;
			var genericArgument = Type.GetGenericArguments()[0];
			var needDefaultConstructor = (genericArgument.GenericParameterAttributes &
			                              GenericParameterAttributes.DefaultConstructorConstraint) != 0;
			return !needDefaultConstructor || type.GetConstructor(Type.EmptyTypes) != null;
		}

		public void UseAsServiceProviderFor(GenericComponent consumer)
		{
			foreach (var dependency in consumer.GenericDependencies)
				if (Type.GetGenericInterfaces().Any(t => dependency.TryMatchWith(t, null)))
					DependenciesToClose.Add(new GenericDependency {Type = dependency, Owner = consumer});
		}

		public void Close(Type[] by, GenericMappingsBuilder builder, ICollection<Type> closedImplementations)
		{
			if (by.Length == 1 && Overrides.Any(x => x.TypeArgument == by[0]))
				return;
			var closedOwner = Type.MakeGenericType(by);
			if (closedImplementations.Contains(closedOwner))
				return;
			var closedInterfaces = closedOwner.GetInterfaces().ToArray();
			foreach (var closedInterface in closedInterfaces)
				builder.DefinedGenericMapping(closedInterface, closedOwner);
			builder.DefinedGenericMapping(closedOwner, closedOwner);
			var interfacesForDependencies = closedOwner.GetGenericInterfaces();
			foreach (var dependency in DependenciesToClose)
				foreach (var interfacesForDependency in interfacesForDependencies)
					dependency.Close(interfacesForDependency, builder, closedImplementations);
			closedImplementations.Add(closedOwner);
		}
	}
}