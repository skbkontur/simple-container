namespace SimpleContainer.Implementation
{
	public struct ActionResult
	{
		public bool isOk;
		public string errorMessage;
	}

	public struct FuncResult<T>
	{
		public T value;
		public bool isOk;
		public string errorMessage;
	}

	public static class Result
	{
		public static FuncResult<T> Fail<T>(string message, params object[] args)
		{
			return new FuncResult<T> {isOk = false, errorMessage = string.Format(message, args)};
		}

		public static FuncResult<T> Ok<T>(T value)
		{
			return new FuncResult<T> {isOk = true, value = value};
		}

		public static ActionResult Fail(string message, params object[] args)
		{
			return new ActionResult {isOk = false, errorMessage = string.Format(message, args)};
		}

		public static ActionResult Ok()
		{
			return new ActionResult {isOk = true};
		}
	}
}