namespace SimpleContainer.Helpers
{
	public static class ValueOrError
	{
		public static ValueOrError<T> Fail<T>(string message, params object[] args)
		{
			return new ValueOrError<T> { isOk = false, errorMessage = string.Format(message, args) };
		}

		public static ValueOrError<T> Ok<T>(T value)
		{
			return new ValueOrError<T> { isOk = true, value = value };
		}
	}

	public struct ValueOrError<T>
	{
		public T value;
		public bool isOk;
		public string errorMessage;
	}
}