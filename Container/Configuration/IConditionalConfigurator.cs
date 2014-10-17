namespace SimpleContainer.Configuration
{
	public interface IConditionalConfigurator
	{
		bool WantsToRun();
	}
}