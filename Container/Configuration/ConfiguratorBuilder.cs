using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using SimpleContainer.Helpers;
using SimpleContainer.Reflection;

namespace SimpleContainer.Configuration
{
	public class ConfiguratorBuilder
	{
		private readonly IContainer container;
		private readonly Type profileType;
		private readonly IDescribeConfigurator configuratorDescriber;
		private IEnumerable<HandlerDescriptor> handlerTypesWithServiceTypes;
		private IEnumerable<IHandleProfile> handlers;

		private ConfiguratorBuilder(IContainer container, IDescribeConfigurator configuratorDescriber, Type profileType)
		{
			this.container = container;
			this.profileType = profileType;
			this.configuratorDescriber = configuratorDescriber;
		}

		private void Initialize()
		{
			handlers = LoadHandlers();
			handlerTypesWithServiceTypes = LoadHandlerTypesWithServiceTypes();
		}

		private IEnumerable<IHandleProfile> LoadHandlers()
		{
			return container
				.GetAll<IHandleProfile>()
				.Where(x => !(x is IConditionalConfigurator) || ((IConditionalConfigurator) x).WantsToRun())
				.ToArray();
		}

		private IEnumerable<HandlerDescriptor> LoadHandlerTypesWithServiceTypes()
		{
			return handlers
				.Select(x => x.GetType())
				.Select(type => new
								{
									HandlerType = type,
									ServiceBinding = GetServiceBinding(type)
								})
				.Where(x => x.ServiceBinding != null)
				.Select(x => new HandlerDescriptor(x.HandlerType, x.ServiceBinding.GetGenericArguments()[0]))
				.ToArray();
		}

		private static Type GetServiceBinding(Type type)
		{
			return type.GetInterfaces()
					   .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof (IConfigure<>));
		}

		public static ConfiguratorBuilder Create(IContainer container, IDescribeConfigurator configuratorDescriber,
												 Type profileType)
		{
			var result = new ConfiguratorBuilder(container, configuratorDescriber, profileType);
			result.Initialize();
			return result;
		}

		public IHandleProfile<IProfile> Build()
		{
			return new CompositeProfileConfigurator(CreateConfigurators());
		}

		private IEnumerable<IHandleProfile<IProfile>> CreateConfigurators()
		{
			IEnumerable<IHandleProfile> filteredHandlers = handlers
				.Where(ApplyFilter)
				.ToArray();
			return profileType
				.ParentsOrSelf()
				.Where(t => typeof (IProfile).IsAssignableFrom(t))
				.Reverse()
				.SelectMany(p => filteredHandlers
									 .Where(h => typeof (IHandleProfile<>).MakeGenericType(p).IsInstanceOfType(h))
									 .Select(h => new
												  {
													  handlerType = h.GetType(),
													  handlerWrap = CreateProfileConfiguratorWrap(h, p)
												  }))
				.OrderBy(t => configuratorDescriber.IsPrimary(t.handlerType))
				.Select(x => x.handlerWrap)
				.ToArray();
		}

		private static IHandleProfile<IProfile> CreateProfileConfiguratorWrap(IHandleProfile handler, Type profileType)
		{
			return (IHandleProfile<IProfile>) Activator.CreateInstance(typeof (ProfileConfigurator<>).MakeGenericType(profileType), handler);
		}

		private bool ApplyFilter(IHandleProfile x)
		{
			var handlerType = x.GetType();
			if (configuratorDescriber.IsPrimary(handlerType))
				return true;
			HandlerDescriptor handlerDescriptor;
			return !handlerTypesWithServiceTypes.TrySingle(y => y.HandlerType == handlerType, out handlerDescriptor) ||
				   !ExistsPrimaryConfiguratorForService(handlerDescriptor);
		}

		private bool ExistsPrimaryConfiguratorForService(HandlerDescriptor handlerDescriptor)
		{
			return handlerTypesWithServiceTypes
				.Any(y => y.HandlerType != handlerDescriptor.HandlerType &&
						  y.ServiceType == handlerDescriptor.ServiceType &&
						  configuratorDescriber.IsPrimary(y.HandlerType));
		}

		private class HandlerDescriptor
		{
			public HandlerDescriptor(Type handlerType, Type serviceType)
			{
				HandlerType = handlerType;
				ServiceType = serviceType;
			}

			public Type HandlerType { get; private set; }
			public Type ServiceType { get; private set; }
		}

		private class ProfileConfigurator<TProfile>: IHandleProfile<IProfile>
			where TProfile: IProfile
		{
			private readonly IHandleProfile<TProfile> handler;

			public ProfileConfigurator(IHandleProfile<TProfile> handler)
			{
				this.handler = handler;
			}

			#region IHandleProfile<IProfile> Members

			public void Handle(NameValueCollection applicationSettings, ContainerConfigurationBuilder builder)
			{
				handler.Handle(applicationSettings, builder);
			}

			#endregion
		}

		private class CompositeProfileConfigurator: IHandleProfile<IProfile>
		{
			private readonly IEnumerable<IHandleProfile<IProfile>> configurators;

			public CompositeProfileConfigurator(IEnumerable<IHandleProfile<IProfile>> configurators)
			{
				this.configurators = configurators;
			}

			#region IHandleProfile<IProfile> Members

			public void Handle(NameValueCollection applicationSettings, ContainerConfigurationBuilder builder)
			{
				configurators.ForEach(profileConfigurator => profileConfigurator.Handle(applicationSettings, builder));
			}

			#endregion
		}
	}
}