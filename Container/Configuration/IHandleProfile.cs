using System.Collections.Specialized;

namespace SimpleContainer.Configuration
{
	public interface IHandleProfile
	{
	}

	public interface IHandleProfile<TProfile>: IHandleProfile
		where TProfile: IProfile
	{
		void Handle(NameValueCollection applicationSettings, ContainerConfigurationBuilder builder);
	}
}