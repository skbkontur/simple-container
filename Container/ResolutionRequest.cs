using System;

namespace SimpleContainer
{
	public class ResolutionRequest
	{
		public Type type;
		public string name;
		public bool createNew;
	}
}