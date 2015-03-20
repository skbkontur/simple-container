namespace SimpleContainer.Implementation
{
	public struct FuncResult<T>
	{
		public T value;
		public bool isOk;
		public string errorMessage;
	}

	public static class FuncResult
	{
		public static FuncResult<T> Fail<T>(string message, params object[] args)
		{
			return new FuncResult<T> {isOk = false, errorMessage = string.Format(message, args)};
		}

		public static FuncResult<T> Ok<T>(T value)
		{
			return new FuncResult<T> {isOk = true, value = value};
		}
	}
}