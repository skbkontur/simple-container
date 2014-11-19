using System;

namespace SimpleContainer.Implementation
{
	public class ResolutionRequest
	{
		public Type type;
		public string name;
		public bool createNew;
	}
}