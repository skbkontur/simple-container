namespace SimpleContainer.Helpers.ReflectionEmit
{
	internal interface IAccessMember
	{
		void Set(object entity, object value);
		object Get(object entity);
	}
}