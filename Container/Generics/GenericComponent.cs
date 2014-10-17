using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Helpers;

namespace SimpleContainer.Generics
{
	public class GenericComponent
	{
		public GenericComponent(Type owner, IEnumerable<Type> genericDependencies, Type[] genericConstraint)
		{
			Owner = owner;
			GenericConstraint = genericConstraint;
			GenericDependencies = genericDependencies.Select(x => new GenericDependency { Type = x, Owner = this }).ToArray();
			Overrides = new List<GenericOverrideInfo>();
			BoundDependencies = new List<GenericDependency>();
		}

		public Type Owner { get; private set; }
		public Type[] GenericConstraint { get; private set; }
		public IEnumerable<GenericDependency> GenericDependencies { get; private set; }
		public List<GenericDependency> BoundDependencies { get; private set; }
		public List<GenericOverrideInfo> Overrides { get; private set; }

		public bool SatisfyConstraints(Type type)
		{
			if (GenericConstraint.Any(c => !c.IsAssignableFrom(type)))
				return false;
			var genericArgument = Owner.GetGenericArguments()[0];
			var needDefaultConstructor = (genericArgument.GenericParameterAttributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0;
			return !needDefaultConstructor || type.GetConstructor(Type.EmptyTypes) != null;
		}

		public void UseAsServiceProviderFor(GenericComponent dependent)
		{
			dependent.GenericDependencies.Where(g => CanClose(g.Type)).ForEach(BoundDependencies.Add);
		}

		public bool CanClose(Type dependency)
		{
			return TypeHelpers.FindAllClosing(dependency, TypeHelpers.GetGenericInterfaces(Owner)).Any();
		}

		public void Close(Type[] by, ContainerConfigurationBuilder builder, ICollection<Type> closedImplementations)
		{
			if (by.Length == 1 && Overrides.Any(x => x.TypeArgument == by[0]))
				return;
			var closedOwner = Owner.MakeGenericType(by);
			if (closedImplementations.Contains(closedOwner))
				return;
			CloseInternal(closedOwner, builder, closedImplementations);
			closedImplementations.Add(closedOwner);
		}

		private void CloseInternal(Type closedOwner, ContainerConfigurationBuilder builder, ICollection<Type> closedImplementations)
		{
			var closedInterfaces = closedOwner.GetInterfaces().ToArray();
			builder.DontUsePluggable(Owner);
			foreach (var closedInterface in closedInterfaces)
			{
				builder.UseAutosearch(closedInterface, true);
				builder.Bind(closedInterface, closedOwner);
			}
			builder.Bind(closedOwner, closedOwner);
			var interfacesForDependencies = TypeHelpers.GetGenericInterfaces(closedOwner);
			foreach (var dependency in BoundDependencies)
				foreach (var c in TypeHelpers.FindAllClosing(dependency.Type, interfacesForDependencies))
					dependency.Close(c, builder, closedImplementations);
		}
	}
}