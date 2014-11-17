namespace SimpleContainer.Hosting
{
	public interface IComponent
	{
		void Run(ComponentHostingOptions options);
	}
}