using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using SimpleContainer.Configuration;

namespace SimpleContainer.Generics
{
	internal class GenericComponent
	{
		public GenericComponent(Type owner, IEnumerable<Type> genericDependencies, Type[] genericConstraint)
		{
			Owner = owner;
			GenericConstraint = genericConstraint;
			GenericDependencies = genericDependencies.ToList();
			Overrides = new List<GenericOverrideInfo>();
			DependenciesToClose = new List<GenericDependency>();
		}

		public Type Owner { get; private set; }
		public Type[] GenericConstraint { get; private set; }
		public List<Type> GenericDependencies { get; private set; }
		public List<GenericDependency> DependenciesToClose { get; private set; }
		public List<GenericOverrideInfo> Overrides { get; private set; }

		public bool SatisfyConstraints(Type type)
		{
			if (GenericConstraint.Any(c => !c.IsAssignableFrom(type)))
				return false;
			var genericArgument = Owner.GetGenericArguments()[0];
			var needDefaultConstructor = (genericArgument.GenericParameterAttributes &
			                              GenericParameterAttributes.DefaultConstructorConstraint) != 0;
			return !needDefaultConstructor || type.GetConstructor(Type.EmptyTypes) != null;
		}

		public void UseAsServiceProviderFor(GenericComponent consumer)
		{
			foreach (var dependency in consumer.GenericDependencies.Where(CanClose))
				DependenciesToClose.Add(new GenericDependency {Type = dependency, Owner = consumer});
		}

		public bool CanClose(Type dependency)
		{
			return TypeHelpers.FindAllClosing(dependency, Owner.GetGenericInterfaces()).Any();
		}

		public void Close(Type[] by, ContainerConfigurationBuilder builder, ICollection<Type> closedImplementations)
		{
			if (by.Length == 1 && Overrides.Any(x => x.TypeArgument == by[0]))
				return;
			var closedOwner = Owner.MakeGenericType(by);
			if (closedImplementations.Contains(closedOwner))
				return;
			var closedInterfaces = closedOwner.GetInterfaces().ToArray();
			foreach (var closedInterface in closedInterfaces)
			{
				builder.UseAutosearch(closedInterface, true);
				builder.Bind(closedInterface, closedOwner);
			}
			builder.Bind(closedOwner, closedOwner);
			var interfacesForDependencies = closedOwner.GetGenericInterfaces();
			foreach (var dependency in DependenciesToClose)
				foreach (var c in TypeHelpers.FindAllClosing(dependency.Type, interfacesForDependencies))
					dependency.Close(c, builder, closedImplementations);
			closedImplementations.Add(closedOwner);
		}
	}
}