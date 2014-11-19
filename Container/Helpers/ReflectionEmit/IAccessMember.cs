namespace SimpleContainer.Helpers.ReflectionEmit
{
	public interface IAccessMember
	{
		void Set(object entity, object value);
		object Get(object entity);
	}
}