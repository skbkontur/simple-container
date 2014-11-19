namespace SimpleContainer.Helpers
{
	public interface IObjectAccessor
	{
		bool TryGet(string name, out object value);
	}
}